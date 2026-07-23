using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using Microsoft.Extensions.Logging;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;

namespace MissionPlanner.Core.Simulation;

/// <summary>Provides conservative direct-SITL model identifiers for supported ArduPilot families.</summary>
public sealed class ArduPilotFrameCatalog : IArduPilotFrameCatalog
{
    private static readonly IReadOnlyDictionary<FirmwareFamily, IReadOnlyList<string>> frames =
        new Dictionary<FirmwareFamily, IReadOnlyList<string>>
        {
            [FirmwareFamily.ArduCopter] = ["quad", "hexa", "octa", "octa-quad", "tri", "y6", "heli"],
            [FirmwareFamily.ArduPlane] = ["plane", "quadplane"],
            [FirmwareFamily.Rover] = ["rover", "balancebot"],
            [FirmwareFamily.ArduSub] = ["vectored", "vectored_6dof"]
        };

    /// <inheritdoc />
    public IReadOnlyList<string> GetFrames(FirmwareFamily family) =>
        frames.TryGetValue(family, out var result) ? result : [];

    /// <inheritdoc />
    public bool IsSupported(FirmwareFamily family, string frameModel) =>
        !string.IsNullOrWhiteSpace(frameModel) &&
        GetFrames(family).Contains(frameModel.Trim(), StringComparer.OrdinalIgnoreCase);
}

/// <summary>Builds typed argument tokens for an ArduPilot direct SITL executable.</summary>
public sealed class ArduPilotLaunchPlanBuilder(IArduPilotFrameCatalog frameCatalog) : IArduPilotLaunchPlanBuilder
{
    private static readonly string[] protectedArguments =
    [
        "--model", "-M", "--home", "-O", "--speedup", "-s", "--instance", "-I",
        "--sysid", "--serial", "--defaults", "--wipe"
    ];

    /// <inheritdoc />
    public ArduPilotLaunchPlan Build(SimulatorProfile profile, string workingDirectory)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (!frameCatalog.IsSupported(profile.FirmwareFamily, profile.FrameModel))
        {
            throw new InvalidOperationException(
                $"Frame/model '{profile.FrameModel}' is not supported for {profile.FirmwareFamily} by the direct SITL adapter.");
        }

        var mavLink = profile.Endpoints.SingleOrDefault(endpoint =>
            endpoint.Name.Equals("MAVLink", StringComparison.OrdinalIgnoreCase));
        if (mavLink is null || mavLink.Transport != SimulationEndpointTransport.Udp)
        {
            throw new InvalidOperationException("A single UDP endpoint named 'MAVLink' is required.");
        }

        var settings = profile.EffectiveLaunchSettings;
        var invariant = CultureInfo.InvariantCulture;
        var home = string.Join(",",
            profile.Location.LatitudeDegrees.ToString("0.#######", invariant),
            profile.Location.LongitudeDegrees.ToString("0.#######", invariant),
            profile.Location.AltitudeMeters.ToString("0.###", invariant),
            profile.Location.HeadingDegrees.ToString("0.###", invariant));
        var arguments = new List<string>
        {
            "--model", profile.FrameModel.Trim(),
            "--home", home,
            "--speedup", profile.Speedup.ToString("0.###", invariant),
            "--instance", settings.Instance.ToString(invariant),
            "--sysid", settings.SystemId.ToString(invariant),
            "--serial0", $"udpclient:{mavLink.Host}:{mavLink.Port}"
        };
        var serialIndices = new HashSet<int>();
        foreach (var serial in settings.EffectiveSerialEndpoints.OrderBy(item => item.Index))
        {
            if (serial.Index is < 1 or > 9 || !serialIndices.Add(serial.Index))
            {
                throw new InvalidOperationException("Additional serial endpoint indices must be unique values from 1 through 9.");
            }

            if (serial.Port is <= 0 or > 65535 || !IsValidEndpointHost(serial.Host))
            {
                throw new InvalidOperationException($"Serial{serial.Index} must have a valid host and port.");
            }

            var transport = serial.Transport switch
            {
                ArduPilotSerialTransport.UdpClient => "udpclient",
                ArduPilotSerialTransport.TcpClient => "tcpclient",
                _ => throw new InvalidOperationException($"Serial{serial.Index} transport is unsupported.")
            };
            arguments.Add($"--serial{serial.Index}");
            arguments.Add($"{transport}:{serial.Host}:{serial.Port}");
        }

        if (settings.DefaultsFiles.Count != 0)
        {
            arguments.Add("--defaults");
            arguments.Add(string.Join(',', settings.DefaultsFiles.Select(Path.GetFullPath)));
        }

        if (settings.WipeState)
        {
            arguments.Add("--wipe");
        }

        foreach (var argument in profile.AdditionalArguments)
        {
            if (string.IsNullOrWhiteSpace(argument))
            {
                throw new InvalidOperationException("Additional SITL argument tokens cannot be empty.");
            }

            if (protectedArguments.Any(item => IsProtectedOverride(item, argument)))
            {
                throw new InvalidOperationException(
                    $"Additional argument '{argument}' attempts to override a typed launch setting.");
            }

            arguments.Add(argument);
        }

        return new ArduPilotLaunchPlan(
            Path.GetFullPath(profile.Binary.ExecutablePath),
            Path.GetFullPath(workingDirectory),
            arguments,
            new Dictionary<string, string>(profile.Environment, StringComparer.OrdinalIgnoreCase),
            mavLink,
            settings.SystemId,
            settings.ShowConsoleWindow);
    }

    private static bool IsProtectedOverride(string protectedArgument, string argument) =>
        argument.Equals(protectedArgument, StringComparison.OrdinalIgnoreCase) ||
        argument.StartsWith(protectedArgument + "=", StringComparison.OrdinalIgnoreCase) ||
        protectedArgument.Equals("--serial", StringComparison.Ordinal) &&
        argument.StartsWith("--serial", StringComparison.OrdinalIgnoreCase) ||
        protectedArgument is "-M" or "-O" or "-s" or "-I" &&
        argument.StartsWith(protectedArgument, StringComparison.OrdinalIgnoreCase);

    private static bool IsValidEndpointHost(string host) =>
        !string.IsNullOrWhiteSpace(host) &&
        !host.Contains(':') &&
        (IPAddress.TryParse(host, out _) || Uri.CheckHostName(host) == UriHostNameType.Dns);
}

/// <summary>Reserves endpoint identities across concurrently owned simulator sessions.</summary>
public sealed class SimulationPortAllocator(ISimulatorHostEnvironment hostEnvironment) : ISimulationPortAllocator
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly HashSet<PortIdentity> claimed = [];

    /// <inheritdoc />
    public async ValueTask<ISimulationPortLease> ReserveAsync(
        IReadOnlyList<SimulationEndpoint> endpoints,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        if (endpoints.Count == 0)
        {
            throw new InvalidOperationException("At least one simulator endpoint must be reserved.");
        }

        var identities = endpoints.Select(PortIdentity.From).ToArray();
        if (identities.Distinct().Count() != identities.Length)
        {
            throw new InvalidOperationException("The simulator profile contains duplicate endpoint reservations.");
        }

        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var collision = identities.FirstOrDefault(claimed.Contains);
            if (collision is not null)
            {
                throw new InvalidOperationException(
                    $"Simulator port {collision.Transport.ToString().ToUpperInvariant()} {collision.Host}:{collision.Port} is already reserved.");
            }

            foreach (var endpoint in endpoints)
            {
                if (!await hostEnvironment.IsPortAvailableAsync(endpoint, cancellationToken).ConfigureAwait(false))
                {
                    throw new InvalidOperationException(
                        $"Simulator endpoint {endpoint.DisplayText} is already in use by another application.");
                }
            }

            claimed.UnionWith(identities);
            return new PortLease(this, endpoints.ToArray(), identities);
        }
        finally
        {
            gate.Release();
        }
    }

    private async ValueTask ReleaseAsync(IReadOnlyList<PortIdentity> identities)
    {
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            claimed.ExceptWith(identities);
        }
        finally
        {
            gate.Release();
        }
    }

    private sealed record PortIdentity(SimulationEndpointTransport Transport, string Host, int Port)
    {
        public static PortIdentity From(SimulationEndpoint endpoint) =>
            new(endpoint.Transport, endpoint.Host.Trim().ToUpperInvariant(), endpoint.Port);
    }

    private sealed class PortLease(
        SimulationPortAllocator owner,
        IReadOnlyList<SimulationEndpoint> endpoints,
        IReadOnlyList<PortIdentity> identities) : ISimulationPortLease
    {
        private int disposed;

        public IReadOnlyList<SimulationEndpoint> Endpoints { get; } = endpoints;

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref disposed, 1) == 0)
            {
                await owner.ReleaseAsync(identities).ConfigureAwait(false);
            }
        }
    }
}

/// <summary>Connects SITL through the existing vehicle connection service and verifies its identity.</summary>
public sealed class SimulatorVehicleConnection(
    IVehicleConnectionService connectionService,
    IVehicleRegistry vehicleRegistry,
    ILogger<SimulatorVehicleConnection> logger) : ISimulatorVehicleConnection
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private Guid? ownedConnectionId;

    /// <inheritdoc />
    public async Task<VehicleId> ConnectAsync(
        SimulatorProfile profile,
        SimulationEndpoint endpoint,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(endpoint);
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (ownedConnectionId is not null)
            {
                throw new SimulationConnectionException("This simulator runtime already owns a vehicle connection.");
            }

            if (connectionService.IsConnected)
            {
                throw new SimulationConnectionException(
                    "MissionPlanner is already connected to a vehicle. Disconnect it before connecting this simulator.");
            }

            using var timeoutCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCancellation.CancelAfter(timeout);
            VehicleConnectionResult result;
            try
            {
                result = await connectionService.ConnectUdpExclusiveAsync(
                    endpoint.Port,
                    endpoint.Host,
                    endpoint.Port,
                    timeoutCancellation.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new SimulationConnectionException(
                    $"The simulator process started, but no MAVLink heartbeat was received within {FormatTimeout(timeout)}.");
            }
            if (!result.Success || result.VehicleId is null || result.ConnectionId is null)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (timeoutCancellation.IsCancellationRequested)
                {
                    throw new SimulationConnectionException(
                        $"The simulator process started, but no MAVLink heartbeat was received within {FormatTimeout(timeout)}.");
                }

                throw new SimulationConnectionException(
                    $"The simulator process started, but MAVLink connection failed: {result.ErrorMessage ?? "no heartbeat was received"}.");
            }

            ownedConnectionId = result.ConnectionId;
            var vehicleId = result.VehicleId.Value;
            var expectedSystemId = profile.EffectiveLaunchSettings.SystemId;
            var state = vehicleRegistry.GetRequired(vehicleId)?.State;
            if (vehicleId.SystemId != expectedSystemId || state?.Identity.Firmware.Family != profile.FirmwareFamily)
            {
                await DisconnectCoreAsync(CancellationToken.None).ConfigureAwait(false);
                throw new SimulationConnectionException(
                    $"Received heartbeat {vehicleId} for {state?.Identity.Firmware.Family.ToString() ?? "unknown firmware"}; " +
                    $"expected system {expectedSystemId} and {profile.FirmwareFamily}.");
            }

            logger.LogInformation(
                "Connected simulator profile {ProfileId} to verified vehicle {VehicleId} using connection {ConnectionId}.",
                profile.Id,
                vehicleId,
                ownedConnectionId);
            return vehicleId;
        }
        finally
        {
            gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await DisconnectCoreAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task DisconnectCoreAsync(CancellationToken cancellationToken)
    {
        if (ownedConnectionId is not { } connectionId)
        {
            return;
        }

        await connectionService.DisconnectOwnedAsync(connectionId, cancellationToken).ConfigureAwait(false);
        ownedConnectionId = null;
    }

    private static string FormatTimeout(TimeSpan timeout) => timeout.TotalSeconds < 1
        ? $"{timeout.TotalMilliseconds:0} milliseconds"
        : $"{timeout.TotalSeconds:0.#} seconds";
}

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
    public ValueTask<IReadOnlyList<SimulationValidationIssue>> ValidateAsync(
        SimulatorProfile profile,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        cancellationToken.ThrowIfCancellationRequested();
        var issues = new List<SimulationValidationIssue>();
        if (!platformService.Current.CanExecuteNative)
        {
            issues.Add(new SimulationValidationIssue(
                "runtime.platform",
                nameof(profile.Binary),
                platformService.Current.Message));
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
    public async Task<ISimulatorRuntimeSession> StartAsync(
        SimulatorStartRequest request,
        CancellationToken cancellationToken = default)
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
                    recentErrors.TryDequeue(out _);
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
