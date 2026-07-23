using MissionPlanner.Core.Vehicles.Models;

namespace MissionPlanner.Core.Simulation;

/// <summary>Provides supported ArduPilot frames/models by firmware family.</summary>
public interface IArduPilotFrameCatalog
{
    /// <summary>Gets the supported direct-SITL model identifiers for a family.</summary>
    /// <param name="family">Firmware family.</param>
    /// <returns>Supported model identifiers.</returns>
    IReadOnlyList<string> GetFrames(FirmwareFamily family);

    /// <summary>Determines whether a frame/model is supported by the direct SITL adapter.</summary>
    /// <param name="family">Firmware family.</param>
    /// <param name="frameModel">Frame/model identifier.</param>
    /// <returns><see langword="true"/> when supported.</returns>
    bool IsSupported(FirmwareFamily family, string frameModel);
}

/// <summary>Builds an ArduPilot direct-binary launch plan from typed profile values.</summary>
public interface IArduPilotLaunchPlanBuilder
{
    /// <summary>Builds a tokenized launch plan without invoking a shell.</summary>
    /// <param name="profile">Validated simulator profile.</param>
    /// <param name="workingDirectory">Absolute isolated session working directory.</param>
    /// <returns>The launch plan.</returns>
    ArduPilotLaunchPlan Build(SimulatorProfile profile, string workingDirectory);
}

/// <summary>Reserves an endpoint set against other MissionPlanner-owned simulator sessions.</summary>
public interface ISimulationPortAllocator
{
    /// <summary>Reserves all profile endpoints as one atomic lease.</summary>
    /// <param name="endpoints">Endpoints to reserve.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>An exact lease released when the runtime ends.</returns>
    ValueTask<ISimulationPortLease> ReserveAsync(
        IReadOnlyList<SimulationEndpoint> endpoints,
        CancellationToken cancellationToken = default);
}

/// <summary>Represents one exact set of MissionPlanner-owned endpoint reservations.</summary>
public interface ISimulationPortLease : IAsyncDisposable
{
    /// <summary>Gets the reserved endpoints.</summary>
    IReadOnlyList<SimulationEndpoint> Endpoints { get; }
}

/// <summary>Starts an exact local process without exposing process APIs to Core.</summary>
public interface ISimulatorProcessHost
{
    /// <summary>Starts a local process from tokenized settings.</summary>
    /// <param name="startInfo">Typed process settings.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The exactly owned process session.</returns>
    Task<ISimulatorProcessSession> StartAsync(
        SimulatorProcessStartInfo startInfo,
        CancellationToken cancellationToken = default);
}

/// <summary>Represents one exact local process owned by a simulator runtime.</summary>
public interface ISimulatorProcessSession : IAsyncDisposable
{
    /// <summary>Gets the operating-system process identifier.</summary>
    int ProcessId { get; }

    /// <summary>Gets process termination.</summary>
    Task<SimulatorRuntimeExit> Completion { get; }

    /// <summary>Gets bounded output captured before downstream observers subscribed.</summary>
    IReadOnlyList<SimulatorOutputLine> RecentOutput { get; }

    /// <summary>Occurs for each complete stdout or stderr line.</summary>
    event EventHandler<SimulatorOutputLine>? OutputReceived;

    /// <summary>Stops this exact process and its descendants.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task StopAsync(CancellationToken cancellationToken = default);
}

/// <summary>Connects an owned simulator endpoint through the existing vehicle connection stack.</summary>
public interface ISimulatorVehicleConnection
{
    /// <summary>Connects and verifies the expected heartbeat identity.</summary>
    /// <param name="profile">Expected simulator profile.</param>
    /// <param name="endpoint">MAVLink listening endpoint.</param>
    /// <param name="timeout">Maximum connection/heartbeat wait.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The exact connected vehicle identity.</returns>
    Task<VehicleId> ConnectAsync(
        SimulatorProfile profile,
        SimulationEndpoint endpoint,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);

    /// <summary>Disconnects only the exact connection owned by this coordinator.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task DisconnectAsync(CancellationToken cancellationToken = default);
}

/// <summary>Contains a tokenized ArduPilot process launch plan.</summary>
/// <param name="ExecutablePath">Absolute executable path.</param>
/// <param name="WorkingDirectory">Isolated session working directory.</param>
/// <param name="Arguments">Individual argument tokens.</param>
/// <param name="Environment">Explicit process environment additions.</param>
/// <param name="ConnectionEndpoint">MissionPlanner MAVLink listening endpoint.</param>
/// <param name="ExpectedSystemId">Expected MAVLink system ID.</param>
/// <param name="ShowConsoleWindow">Whether a visible desktop console is requested.</param>
public sealed record ArduPilotLaunchPlan(
    string ExecutablePath,
    string WorkingDirectory,
    IReadOnlyList<string> Arguments,
    IReadOnlyDictionary<string, string> Environment,
    SimulationEndpoint ConnectionEndpoint,
    byte ExpectedSystemId,
    bool ShowConsoleWindow);

/// <summary>Contains platform-neutral local process start settings.</summary>
/// <param name="ExecutablePath">Absolute executable path.</param>
/// <param name="WorkingDirectory">Absolute isolated working directory.</param>
/// <param name="Arguments">Individual process argument tokens.</param>
/// <param name="Environment">Explicit environment additions.</param>
/// <param name="ShowConsoleWindow">Whether a visible desktop console is requested.</param>
public sealed record SimulatorProcessStartInfo(
    string ExecutablePath,
    string WorkingDirectory,
    IReadOnlyList<string> Arguments,
    IReadOnlyDictionary<string, string> Environment,
    bool ShowConsoleWindow);

/// <summary>Signals an actionable simulator-to-vehicle connection failure.</summary>
public sealed class SimulationConnectionException : Exception
{
    /// <summary>Initializes a simulation connection failure.</summary>
    /// <param name="message">Actionable failure detail.</param>
    public SimulationConnectionException(string message)
        : base(message)
    {
    }
}
