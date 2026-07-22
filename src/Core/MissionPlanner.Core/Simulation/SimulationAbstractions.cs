namespace MissionPlanner.Core.Simulation;

/// <summary>Persists an opaque, non-secret simulator-profile document.</summary>
public interface ISimulatorProfileStore
{
    /// <summary>Reads the persisted profile document.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The document, or <see langword="null"/> when no document exists.</returns>
    ValueTask<string?> ReadAsync(CancellationToken cancellationToken = default);

    /// <summary>Atomically replaces the persisted profile document.</summary>
    /// <param name="document">The serialized document.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    ValueTask WriteAsync(string document, CancellationToken cancellationToken = default);
}

/// <summary>Loads and persists simulator profiles.</summary>
public interface ISimulatorProfileService
{
    /// <summary>Gets the initialized profile collection.</summary>
    IReadOnlyList<SimulatorProfile> Profiles { get; }

    /// <summary>Loads persisted profiles, recovering to a safe default when necessary.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The initialized profiles.</returns>
    ValueTask<IReadOnlyList<SimulatorProfile>> InitializeAsync(CancellationToken cancellationToken = default);

    /// <summary>Adds or replaces a profile and persists the complete collection.</summary>
    /// <param name="profile">The profile.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    ValueTask SaveAsync(SimulatorProfile profile, CancellationToken cancellationToken = default);

    /// <summary>Deletes a profile and ensures at least one default remains.</summary>
    /// <param name="profileId">The profile identity.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    ValueTask DeleteAsync(Guid profileId, CancellationToken cancellationToken = default);
}

/// <summary>Provides host-specific validation without coupling the workspace to a runtime implementation.</summary>
public interface ISimulatorHostEnvironment
{
    /// <summary>Validates whether an executable path exists and is executable on this host.</summary>
    /// <param name="executablePath">Absolute executable path.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A validation issue, or <see langword="null"/> when valid.</returns>
    ValueTask<SimulationValidationIssue?> ValidateExecutableAsync(
        string executablePath,
        CancellationToken cancellationToken = default);

    /// <summary>Checks whether a requested endpoint port is currently available.</summary>
    /// <param name="endpoint">The requested endpoint.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> when the port is currently available.</returns>
    ValueTask<bool> IsPortAvailableAsync(
        SimulationEndpoint endpoint,
        CancellationToken cancellationToken = default);
}

/// <summary>Validates simulator profiles and host resources before runtime creation.</summary>
public interface ISimulatorProfileValidator
{
    /// <summary>Validates a profile for launch.</summary>
    /// <param name="profile">The profile.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>All detected validation issues.</returns>
    ValueTask<IReadOnlyList<SimulationValidationIssue>> ValidateAsync(
        SimulatorProfile profile,
        CancellationToken cancellationToken = default);
}

/// <summary>Describes a runtime start request without exposing process APIs.</summary>
/// <param name="SessionId">MissionPlanner-owned session identity.</param>
/// <param name="Profile">Validated launch profile.</param>
/// <param name="LogDirectory">Session-specific log directory.</param>
public sealed record SimulatorStartRequest(Guid SessionId, SimulatorProfile Profile, string LogDirectory);

/// <summary>Defines a process-, container-, or remote-neutral simulator runtime adapter.</summary>
public interface ISimulatorRuntime
{
    /// <summary>Gets the runtime adapter name.</summary>
    string Name { get; }

    /// <summary>Performs runtime-specific validation without starting a session.</summary>
    /// <param name="profile">The structurally valid profile.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Runtime-specific validation issues.</returns>
    ValueTask<IReadOnlyList<SimulationValidationIssue>> ValidateAsync(
        SimulatorProfile profile,
        CancellationToken cancellationToken = default);

    /// <summary>Starts and returns one exactly identified owned runtime session.</summary>
    /// <param name="request">The typed start request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The owned runtime session.</returns>
    Task<ISimulatorRuntimeSession> StartAsync(
        SimulatorStartRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>Represents one exact runtime session owned by MissionPlanner.</summary>
public interface ISimulatorRuntimeSession : IAsyncDisposable
{
    /// <summary>Gets the exact runtime identity used for safe cleanup.</summary>
    SimulatorRuntimeIdentity Identity { get; }

    /// <summary>Gets the runtime-confirmed connection endpoints.</summary>
    IReadOnlyList<SimulationEndpoint> ConnectionEndpoints { get; }

    /// <summary>Gets runtime termination.</summary>
    Task<SimulatorRuntimeExit> Completion { get; }

    /// <summary>Occurs when the runtime emits one complete output line.</summary>
    event EventHandler<SimulatorOutputLine>? OutputReceived;

    /// <summary>Waits for the expected simulator heartbeat or readiness signal.</summary>
    /// <param name="timeout">Maximum wait.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task WaitForHeartbeatAsync(TimeSpan timeout, CancellationToken cancellationToken = default);

    /// <summary>Stops only this exactly identified owned runtime session.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task StopAsync(CancellationToken cancellationToken = default);
}

/// <summary>Coordinates one observable simulation session.</summary>
public interface ISimulationSessionManager : IAsyncDisposable
{
    /// <summary>Gets the current immutable session snapshot.</summary>
    SimulationSessionSnapshot Current { get; }

    /// <summary>Occurs after the current simulation state changes.</summary>
    event EventHandler<SimulationSessionChangedEventArgs>? Changed;

    /// <summary>Validates and starts a simulator profile.</summary>
    /// <param name="profile">The selected profile.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The resulting state.</returns>
    Task<SimulationSessionSnapshot> StartAsync(
        SimulatorProfile profile,
        CancellationToken cancellationToken = default);

    /// <summary>Stops the exact currently owned runtime session.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The resulting state.</returns>
    Task<SimulationSessionSnapshot> StopAsync(CancellationToken cancellationToken = default);

    /// <summary>Stops and starts the last selected profile.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The resulting state.</returns>
    Task<SimulationSessionSnapshot> RestartAsync(CancellationToken cancellationToken = default);

    /// <summary>Stops all runtime resources owned by the workspace during application shutdown.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task ShutdownAsync(CancellationToken cancellationToken = default);
}

/// <summary>Builds a non-secret diagnostic document for a simulation session.</summary>
public interface ISimulationDiagnosticsService
{
    /// <summary>Creates a structured diagnostic bundle.</summary>
    /// <param name="snapshot">The session snapshot.</param>
    /// <returns>A redacted JSON document.</returns>
    string CreateBundle(SimulationSessionSnapshot snapshot);
}

/// <summary>Configures simulation workspace lifecycle limits.</summary>
public sealed class SimulationWorkspaceOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Simulation";

    /// <summary>Gets or sets the maximum heartbeat wait in seconds.</summary>
    public int HeartbeatTimeoutSeconds { get; set; } = 20;

    /// <summary>Gets or sets the maximum graceful stop wait in seconds.</summary>
    public int StopTimeoutSeconds { get; set; } = 10;

    /// <summary>Gets or sets the number of recent output lines retained in memory.</summary>
    public int RecentOutputCapacity { get; set; } = 500;

    /// <summary>Gets or sets the root directory for per-session runtime logs.</summary>
    public string LogRootDirectory { get; set; } = Path.Combine(Path.GetTempPath(), "MissionPlanner", "Simulation");
}
