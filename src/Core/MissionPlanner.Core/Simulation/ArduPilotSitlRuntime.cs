using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Simulation;
using MissionPlanner.Simulation.Abstractions;

namespace MissionPlanner.Core.Simulation;

/// <summary>Launches one direct ArduPilot SITL process and connects it to MissionPlanner.</summary>
public sealed class ArduPilotSitlRuntime(
    IArduPilotLaunchPlanBuilder launchPlanBuilder,
    IArduPilotFrameCatalog frameCatalog,
    ISimulationPortAllocator portAllocator,
    ISimulatorProcessHost processHost,
    ISimulationOwnershipStore ownershipStore,
    ISimulatorVehicleConnectionFactory vehicleConnectionFactory,
    ISitlPlatformService platformService,
    ILogger<ArduPilotSitlRuntime> logger) : ISimulatorRuntime
{
    /// <inheritdoc />
    public string Name => "ArduPilot direct SITL";

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<SimulationValidationIssue>> ValidateAsync(SimulatorProfile profile, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        cancellationToken.ThrowIfCancellationRequested();
        var issues = new List<SimulationValidationIssue>();
        if (!platformService.Current.CanExecuteNative)
        {
            issues.Add(new SimulationValidationIssue("runtime.platform", nameof(profile.Binary), platformService.Current.Message));
        }

        if (!frameCatalog.IsSupported(profile.FirmwareFamily, profile.FrameModel))
        {
            issues.Add(new SimulationValidationIssue(
                "runtime.frame",
                nameof(profile.FrameModel),
                $"Frame/model '{profile.FrameModel}' is not supported for {profile.FirmwareFamily}. " +
                $"Supported values: {string.Join(", ", frameCatalog.GetFrames(profile.FirmwareFamily))}."));
        }

        var settings = profile.EffectiveLaunchSettings;
        if (settings.Instance is < 0 or > 254)
        {
            issues.Add(new SimulationValidationIssue(
                "runtime.instance",
                nameof(settings.Instance),
                "SITL instance must be between 0 and 254."));
        }

        if (settings.SystemId == 0)
        {
            issues.Add(new SimulationValidationIssue(
                "runtime.system-id",
                nameof(settings.SystemId),
                "MAVLink SystemId must be between 1 and 255."));
        }

        foreach (var defaultsFile in settings.DefaultsFiles)
        {
            if (!Path.IsPathFullyQualified(defaultsFile) || !File.Exists(defaultsFile))
            {
                issues.Add(new SimulationValidationIssue(
                    "runtime.defaults",
                    nameof(settings.DefaultsFiles),
                    $"Defaults file '{defaultsFile}' must be an existing absolute file."));
            }
        }

        var mavLinkEndpoints = profile.Endpoints.Where(endpoint =>
            endpoint.Name.Equals("MAVLink", StringComparison.OrdinalIgnoreCase) &&
            endpoint.Transport == SimulationEndpointTransport.Udp).ToArray();
        if (mavLinkEndpoints.Length != 1 || !mavLinkEndpoints[0].Host.Equals("127.0.0.1", StringComparison.Ordinal))
        {
            issues.Add(new SimulationValidationIssue(
                "runtime.mavlink-endpoint",
                nameof(profile.Endpoints),
                "Direct SITL requires exactly one UDP MAVLink endpoint on 127.0.0.1."));
        }

        try
        {
            _ = launchPlanBuilder.Build(profile, Path.GetTempPath());
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException)
        {
            issues.Add(new SimulationValidationIssue("runtime.arguments", nameof(profile.AdditionalArguments), exception.Message));
        }

        return ValueTask.FromResult<IReadOnlyList<SimulationValidationIssue>>(issues);
    }

    /// <inheritdoc />
    public async Task<ISimulatorRuntimeSession> StartAsync(SimulatorStartRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var vehicleConnection = vehicleConnectionFactory.Create(request.SessionId);
        var plan = launchPlanBuilder.Build(request.Profile, request.LogDirectory);
        var lease = await portAllocator.ReserveAsync(request.Profile.Endpoints, cancellationToken).ConfigureAwait(false);
        ISimulatorProcessSession? process = null;
        try
        {
            Directory.CreateDirectory(plan.WorkingDirectory);
            process = await processHost.StartAsync(
                new SimulatorProcessStartInfo(
                    plan.ExecutablePath,
                    plan.WorkingDirectory,
                    plan.Arguments,
                    plan.Environment,
                    plan.ShowConsoleWindow),
                cancellationToken).ConfigureAwait(false);
            await ownershipStore.MarkAsync(
                new SimulationOwnedProcess(
                    request.SessionId,
                    Guid.NewGuid(),
                    process.ProcessId,
                    process.ExecutablePath,
                    process.StartedAt),
                cancellationToken).ConfigureAwait(false);
            logger.LogInformation(
                "Started ArduPilot SITL process {ProcessId} for session {SessionId}, instance {Instance}, SystemId {SystemId}.",
                process.ProcessId,
                request.SessionId,
                request.Profile.EffectiveLaunchSettings.Instance,
                request.Profile.EffectiveLaunchSettings.SystemId);
            return new ArduPilotRuntimeSession(
                request.SessionId,
                request.Profile,
                plan,
                process,
                lease,
                vehicleConnection,
                ownershipStore);
        }
        catch
        {
            if (process is not null)
            {
                try
                {
                    await process.StopAsync(CancellationToken.None).ConfigureAwait(false);
                    await process.DisposeAsync().ConfigureAwait(false);
                }
                catch (Exception cleanupException)
                {
                    logger.LogWarning(cleanupException, "Failed to clean up SITL after startup coordination failed.");
                }

                await ownershipStore.ReleaseAsync(request.SessionId, CancellationToken.None).ConfigureAwait(false);
            }

            await lease.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    private sealed class ArduPilotRuntimeSession : ISimulatorRuntimeSession
    {
        private readonly SimulatorProfile profile;
        private readonly ArduPilotLaunchPlan plan;
        private readonly ISimulatorProcessSession process;
        private readonly ISimulationPortLease portLease;
        private readonly ISimulatorVehicleConnection vehicleConnection;
        private readonly ISimulationOwnershipStore ownershipStore;
        private readonly Guid sessionId;
        private readonly ConcurrentQueue<string> recentErrors = new();
        private readonly SemaphoreSlim cleanupGate = new(1, 1);
        private readonly Task<SimulatorRuntimeExit> completion;
        private bool stopRequested;
        private bool cleanedUp;

        public ArduPilotRuntimeSession(
            Guid sessionId,
            SimulatorProfile profile,
            ArduPilotLaunchPlan plan,
            ISimulatorProcessSession process,
            ISimulationPortLease portLease,
            ISimulatorVehicleConnection vehicleConnection,
            ISimulationOwnershipStore ownershipStore)
        {
            this.sessionId = sessionId;
            this.profile = profile;
            this.plan = plan;
            this.process = process;
            this.portLease = portLease;
            this.vehicleConnection = vehicleConnection;
            this.ownershipStore = ownershipStore;
            Identity = new SimulatorRuntimeIdentity(
                $"sitl-{sessionId:N}-{process.ProcessId}",
                "ArduPilot direct SITL",
                process.ProcessId);
            Diagnostics = new SimulationRuntimeDiagnostics(
                plan.ExecutablePath,
                plan.Arguments,
                profile.Binary.Version,
                process.StartedAt,
                new SimulationHeartbeatStatistics(
                    plan.ExpectedSystemId,
                    null,
                    null,
                    null,
                    0));
            foreach (var line in process.RecentOutput.Where(line => line.Stream == SimulatorOutputStream.StandardError))
            {
                recentErrors.Enqueue(line.Text);
            }

            process.OutputReceived += OnProcessOutput;
            completion = ObserveCompletionAsync();
        }

        public SimulatorRuntimeIdentity Identity { get; }

        public VehicleId? ConnectedVehicleId { get; private set; }

        public IReadOnlyList<SimulationEndpoint> ConnectionEndpoints => portLease.Endpoints;

        public SimulationRuntimeDiagnostics? Diagnostics { get; private set; }

        public Task<SimulatorRuntimeExit> Completion => completion;

        public event EventHandler<SimulatorOutputLine>? OutputReceived;

        public async Task WaitForHeartbeatAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            using var readinessCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var connectTask = vehicleConnection.ConnectAsync(
                profile,
                plan.ConnectionEndpoint,
                timeout,
                readinessCancellation.Token);
            var completed = await Task.WhenAny(connectTask, process.Completion).ConfigureAwait(false);
            if (completed == process.Completion)
            {
                readinessCancellation.Cancel();
                try
                {
                    await connectTask.ConfigureAwait(false);
                }
                catch (Exception) when (readinessCancellation.IsCancellationRequested)
                {
                }

                var exit = await process.Completion.ConfigureAwait(false);
                throw new SimulationConnectionException(
                    $"SITL exited before vehicle connection completed (exit {exit.ExitCode?.ToString() ?? "unknown"}).{ErrorSuffix()}");
            }

            try
            {
                ConnectedVehicleId = await connectTask.ConfigureAwait(false);
                var observedAt = DateTimeOffset.UtcNow;
                Diagnostics = Diagnostics! with
                {
                    Heartbeat = new SimulationHeartbeatStatistics(
                        plan.ExpectedSystemId,
                        ConnectedVehicleId,
                        observedAt,
                        observedAt >= process.StartedAt ? observedAt - process.StartedAt : TimeSpan.Zero,
                        1)
                };
            }
            catch (Exception exception) when (exception is SimulationConnectionException or OperationCanceledException)
            {
                throw new SimulationConnectionException(exception.Message + ErrorSuffix());
            }
        }

        public async Task StopAsync(CancellationToken cancellationToken = default)
        {
            await cleanupGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (cleanedUp)
                {
                    return;
                }

                stopRequested = true;
                var failures = new List<Exception>();
                try
                {
                    await vehicleConnection.DisconnectAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    failures.Add(exception);
                }

                try
                {
                    await process.StopAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    failures.Add(exception);
                }

                try
                {
                    await portLease.DisposeAsync().ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    failures.Add(exception);
                }

                try
                {
                    await ownershipStore.ReleaseAsync(sessionId, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    failures.Add(exception);
                }

                cleanedUp = failures.Count == 0;
                if (failures.Count != 0)
                {
                    throw new AggregateException("One or more owned SITL resources could not be stopped cleanly.", failures);
                }
            }
            finally
            {
                cleanupGate.Release();
            }
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                await StopAsync().ConfigureAwait(false);
            }
            finally
            {
                process.OutputReceived -= OnProcessOutput;
                await process.DisposeAsync().ConfigureAwait(false);
            }
        }

        private async Task<SimulatorRuntimeExit> ObserveCompletionAsync()
        {
            var exit = await process.Completion.ConfigureAwait(false);
            return exit with { WasExpected = exit.WasExpected || stopRequested };
        }

        private void OnProcessOutput(object? sender, SimulatorOutputLine line)
        {
            if (line.Stream == SimulatorOutputStream.StandardError)
            {
                recentErrors.Enqueue(line.Text);
                while (recentErrors.Count > 12)
                {
                    recentErrors.TryDequeue(out var _);
                }
            }

            OutputReceived?.Invoke(this, line);
        }

        private string ErrorSuffix()
        {
            var errors = recentErrors.ToArray();
            return errors.Length == 0 ? string.Empty : $" Recent stderr: {string.Join(" | ", errors)}";
        }
    }
}
