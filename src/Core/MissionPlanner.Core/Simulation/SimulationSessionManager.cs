using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MissionPlanner.Library.DateTime.Domain;

namespace MissionPlanner.Core.Simulation;

/// <summary>Coordinates validation and lifecycle for one exactly owned simulator runtime session.</summary>
public sealed class SimulationSessionManager : ISimulationSessionManager
{
    private readonly ISimulatorProfileValidator profileValidator;
    private readonly ISimulatorRuntime runtime;
    private readonly IDateTimeProvider clock;
    private readonly ILogger<SimulationSessionManager> logger;
    private readonly SimulationWorkspaceOptions options;
    private readonly SemaphoreSlim operationGate = new(1, 1);
    private readonly object stateLock = new();
    private readonly Queue<SimulatorOutputLine> recentOutput = new();
    private SimulationSessionSnapshot current = SimulationSessionSnapshot.Stopped;
    private ISimulatorRuntimeSession? runtimeSession;
    private CancellationTokenSource? lifecycleCancellation;
    private SimulatorProfile? lastProfile;
    private bool disposed;

    /// <summary>Initializes the simulation session manager.</summary>
    /// <param name="profileValidator">The profile and host-resource validator.</param>
    /// <param name="runtime">The selected runtime adapter.</param>
    /// <param name="clock">The application clock.</param>
    /// <param name="options">Lifecycle limits.</param>
    /// <param name="logger">The logger.</param>
    public SimulationSessionManager(
        ISimulatorProfileValidator profileValidator,
        ISimulatorRuntime runtime,
        IDateTimeProvider clock,
        IOptions<SimulationWorkspaceOptions> options,
        ILogger<SimulationSessionManager> logger)
    {
        this.profileValidator = profileValidator;
        this.runtime = runtime;
        this.clock = clock;
        this.options = options.Value;
        this.logger = logger;
    }

    /// <inheritdoc />
    public SimulationSessionSnapshot Current
    {
        get
        {
            lock (stateLock)
            {
                return current;
            }
        }
    }

    /// <inheritdoc />
    public event EventHandler<SimulationSessionChangedEventArgs>? Changed;

    /// <inheritdoc />
    public async Task<SimulationSessionSnapshot> StartAsync(
        SimulatorProfile profile,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ObjectDisposedException.ThrowIf(disposed, this);
        await operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (runtimeSession is not null)
            {
                return Publish(current with
                {
                    Message = "Stop the current simulation before starting another profile."
                });
            }

            var sessionId = Guid.NewGuid();
            var artifacts = SimulationInstanceArtifacts.Create(
                options.LogRootDirectory,
                profile.EffectiveLaunchSettings.Instance,
                profile.EffectiveLaunchSettings.SystemId);
            lastProfile = profile;
            recentOutput.Clear();
            Publish(new SimulationSessionSnapshot(
                sessionId,
                profile,
                SimulationSessionState.Validating,
                null,
                profile.Endpoints,
                null,
                null,
                $"Validating profile '{profile.Name}'.",
                null,
                [],
                Artifacts: artifacts));

            var issues = (await profileValidator.ValidateAsync(profile, cancellationToken).ConfigureAwait(false)).ToList();
            issues.AddRange(await runtime.ValidateAsync(profile, cancellationToken).ConfigureAwait(false));
            if (issues.Count != 0)
            {
                var failure = string.Join(Environment.NewLine, issues.Select(issue => issue.Message));
                logger.LogWarning(
                    "Simulation profile validation failed for {ProfileId} with {IssueCount} issue(s).",
                    profile.Id,
                    issues.Count);
                return Publish(current with
                {
                    State = SimulationSessionState.Failed,
                    EndedAt = clock.UtcNow,
                    Message = "Simulation profile validation failed.",
                    Failure = failure
                });
            }

            Publish(current with
            {
                State = SimulationSessionState.Starting,
                Message = $"Starting with runtime adapter '{runtime.Name}'."
            });
            lifecycleCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            artifacts.CreateDirectories();
            var createdSession = await runtime.StartAsync(
                new SimulatorStartRequest(sessionId, profile, artifacts.RuntimeLogDirectory),
                lifecycleCancellation.Token).ConfigureAwait(false);
            runtimeSession = createdSession;
            createdSession.OutputReceived += OnOutputReceived;
            Publish(current with
            {
                State = SimulationSessionState.WaitingForHeartbeat,
                RuntimeIdentity = createdSession.Identity,
                ConnectionEndpoints = createdSession.ConnectionEndpoints,
                StartedAt = clock.UtcNow,
                Message = "Runtime started; waiting for the expected vehicle heartbeat."
            });

            var heartbeatTimeout = TimeSpan.FromSeconds(Math.Max(1, options.HeartbeatTimeoutSeconds));
            await createdSession.WaitForHeartbeatAsync(heartbeatTimeout, lifecycleCancellation.Token).ConfigureAwait(false);
            Publish(current with
            {
                State = SimulationSessionState.Running,
                Message = "Simulator is running and the expected heartbeat was observed.",
                VehicleId = createdSession.ConnectedVehicleId
            });
            logger.LogInformation(
                "Simulation session {SessionId} is running with runtime {RuntimeId} for profile {ProfileId}.",
                sessionId,
                createdSession.Identity.RuntimeId,
                profile.Id);
            _ = ObserveCompletionAsync(createdSession, sessionId);
            return Current;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested || lifecycleCancellation?.IsCancellationRequested == true)
        {
            await CleanupCreatedSessionAsync().ConfigureAwait(false);
            Publish(current with
            {
                State = SimulationSessionState.Stopped,
                EndedAt = clock.UtcNow,
                Message = "Simulation start was cancelled.",
                Failure = null
            });
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Simulation session failed during startup for profile {ProfileId}.", profile.Id);
            await CleanupCreatedSessionAsync().ConfigureAwait(false);
            return Publish(current with
            {
                State = SimulationSessionState.Failed,
                EndedAt = clock.UtcNow,
                Message = "Simulation startup failed.",
                Failure = exception.Message
            });
        }
        finally
        {
            operationGate.Release();
        }
    }

    /// <inheritdoc />
    public async Task<SimulationSessionSnapshot> StopAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        lifecycleCancellation?.Cancel();
        await operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var ownedSession = runtimeSession;
            if (ownedSession is null)
            {
                return Publish(current with
                {
                    State = SimulationSessionState.Stopped,
                    EndedAt = current.StartedAt is null ? null : clock.UtcNow,
                    Message = "No simulation is running.",
                    Failure = null
                });
            }

            Publish(current with
            {
                State = SimulationSessionState.Stopping,
                Message = $"Stopping owned runtime '{ownedSession.Identity.RuntimeId}'."
            });
            ownedSession.OutputReceived -= OnOutputReceived;
            using var stopCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            stopCancellation.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, options.StopTimeoutSeconds)));
            try
            {
                await ownedSession.StopAsync(stopCancellation.Token).ConfigureAwait(false);
                await ownedSession.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Owned simulation runtime {RuntimeId} could not be stopped cleanly.",
                    ownedSession.Identity.RuntimeId);
                try
                {
                    await ownedSession.DisposeAsync().ConfigureAwait(false);
                }
                catch (Exception disposeException)
                {
                    logger.LogWarning(
                        disposeException,
                        "Owned simulation runtime {RuntimeId} also failed during disposal.",
                        ownedSession.Identity.RuntimeId);
                }

                runtimeSession = null;
                DisposeLifecycleCancellation();
                var failed = Publish(current with
                {
                    State = SimulationSessionState.Failed,
                    EndedAt = clock.UtcNow,
                    Message = "The owned simulator did not stop cleanly.",
                    Failure = exception.Message
                });
                cancellationToken.ThrowIfCancellationRequested();
                return failed;
            }

            runtimeSession = null;
            DisposeLifecycleCancellation();
            logger.LogInformation(
                "Stopped owned simulation session {SessionId} with runtime {RuntimeId}.",
                current.SessionId,
                ownedSession.Identity.RuntimeId);
            return Publish(current with
            {
                State = SimulationSessionState.Stopped,
                EndedAt = clock.UtcNow,
                Message = "Simulation stopped.",
                Failure = null
            });
        }
        finally
        {
            operationGate.Release();
        }
    }

    /// <inheritdoc />
    public async Task<SimulationSessionSnapshot> RestartAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        var profile = Current.Profile ?? lastProfile;
        if (profile is null)
        {
            return Publish(Current with
            {
                State = SimulationSessionState.Failed,
                EndedAt = clock.UtcNow,
                Message = "Simulation restart failed.",
                Failure = "No simulation profile has been selected."
            });
        }

        await StopAsync(cancellationToken).ConfigureAwait(false);
        return await StartAsync(profile, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task ShutdownAsync(CancellationToken cancellationToken = default)
    {
        if (disposed)
        {
            return;
        }

        logger.LogInformation("Simulation workspace shutdown requested.");
        await StopAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        await ShutdownAsync().ConfigureAwait(false);
        disposed = true;
        operationGate.Dispose();
    }

    private async Task ObserveCompletionAsync(ISimulatorRuntimeSession ownedSession, Guid sessionId)
    {
        SimulatorRuntimeExit exit;
        try
        {
            exit = await ownedSession.Completion.ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            exit = new SimulatorRuntimeExit(null, false, exception.Message);
        }

        await operationGate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!ReferenceEquals(runtimeSession, ownedSession) || Current.SessionId != sessionId)
            {
                return;
            }

            ownedSession.OutputReceived -= OnOutputReceived;
            runtimeSession = null;
            DisposeLifecycleCancellation();
            await ownedSession.DisposeAsync().ConfigureAwait(false);
            var successful = exit.WasExpected && exit.ExitCode is null or 0;
            Publish(current with
            {
                State = successful ? SimulationSessionState.Completed : SimulationSessionState.Failed,
                EndedAt = clock.UtcNow,
                Message = successful ? "Simulation completed." : "Simulation runtime exited unexpectedly.",
                Failure = successful ? null : exit.Message ?? $"Runtime exit code: {exit.ExitCode?.ToString() ?? "unavailable"}."
            });
            logger.LogInformation(
                "Simulation session {SessionId} ended in state {State} with exit code {ExitCode}.",
                sessionId,
                Current.State,
                exit.ExitCode);
        }
        finally
        {
            operationGate.Release();
        }
    }

    private void OnOutputReceived(object? sender, SimulatorOutputLine line)
    {
        SimulationSessionSnapshot snapshot;
        lock (stateLock)
        {
            recentOutput.Enqueue(line);
            var capacity = Math.Max(1, options.RecentOutputCapacity);
            while (recentOutput.Count > capacity)
            {
                recentOutput.Dequeue();
            }

            current = current with { RecentOutput = recentOutput.ToArray() };
            snapshot = current;
        }

        Changed?.Invoke(this, new SimulationSessionChangedEventArgs(snapshot));
    }

    private SimulationSessionSnapshot Publish(SimulationSessionSnapshot snapshot)
    {
        lock (stateLock)
        {
            current = snapshot with { RecentOutput = recentOutput.ToArray() };
            snapshot = current;
        }

        Changed?.Invoke(this, new SimulationSessionChangedEventArgs(snapshot));
        return snapshot;
    }

    private async Task CleanupCreatedSessionAsync()
    {
        var ownedSession = runtimeSession;
        runtimeSession = null;
        DisposeLifecycleCancellation();
        if (ownedSession is null)
        {
            return;
        }

        ownedSession.OutputReceived -= OnOutputReceived;
        using var cleanupCancellation = new CancellationTokenSource(
            TimeSpan.FromSeconds(Math.Max(1, options.StopTimeoutSeconds)));
        try
        {
            await ownedSession.StopAsync(cleanupCancellation.Token).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Cleanup of owned simulation runtime {RuntimeId} failed.",
                ownedSession.Identity.RuntimeId);
        }
        finally
        {
            try
            {
                await ownedSession.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                logger.LogWarning(
                    exception,
                    "Disposal of owned simulation runtime {RuntimeId} failed during startup cleanup.",
                    ownedSession.Identity.RuntimeId);
            }
        }
    }

    private void DisposeLifecycleCancellation()
    {
        lifecycleCancellation?.Dispose();
        lifecycleCancellation = null;
    }
}
