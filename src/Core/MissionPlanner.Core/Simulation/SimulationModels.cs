using MissionPlanner.Core.Vehicles.Models;

namespace MissionPlanner.Core.Simulation;

/// <summary>Identifies a simulation session lifecycle state.</summary>
public enum SimulationSessionState
{
    /// <summary>No simulator session is active.</summary>
    Stopped,

    /// <summary>The selected profile and host resources are being validated.</summary>
    Validating,

    /// <summary>The runtime is creating the owned simulator session.</summary>
    Starting,

    /// <summary>The runtime is active and MissionPlanner is waiting for its heartbeat.</summary>
    WaitingForHeartbeat,

    /// <summary>The simulator is running and its expected heartbeat was observed.</summary>
    Running,

    /// <summary>The owned simulator session is stopping.</summary>
    Stopping,

    /// <summary>The simulator exited successfully without an explicit stop request.</summary>
    Completed,

    /// <summary>Validation, startup, heartbeat, runtime, or cleanup failed.</summary>
    Failed
}

/// <summary>Identifies a simulator output channel.</summary>
public enum SimulatorOutputStream
{
    /// <summary>Standard output from the runtime.</summary>
    StandardOutput,

    /// <summary>Standard error from the runtime.</summary>
    StandardError,

    /// <summary>A lifecycle message produced by the simulation coordinator.</summary>
    System
}

/// <summary>Identifies an endpoint transport used by a simulation profile.</summary>
public enum SimulationEndpointTransport
{
    /// <summary>UDP endpoint.</summary>
    Udp,

    /// <summary>TCP endpoint.</summary>
    Tcp
}

/// <summary>Describes the simulation start location.</summary>
/// <param name="LatitudeDegrees">Latitude in decimal degrees.</param>
/// <param name="LongitudeDegrees">Longitude in decimal degrees.</param>
/// <param name="AltitudeMeters">Altitude above mean sea level in meters.</param>
/// <param name="HeadingDegrees">Initial heading in degrees.</param>
public sealed record SimulationLocation(
    double LatitudeDegrees,
    double LongitudeDegrees,
    double AltitudeMeters,
    double HeadingDegrees);

/// <summary>Describes one named simulator endpoint.</summary>
/// <param name="Name">Stable endpoint role, such as MAVLink or console.</param>
/// <param name="Transport">Endpoint transport.</param>
/// <param name="Host">Host or bind address.</param>
/// <param name="Port">IP port.</param>
public sealed record SimulationEndpoint(
    string Name,
    SimulationEndpointTransport Transport,
    string Host,
    int Port)
{
    /// <summary>Gets a user-facing endpoint description.</summary>
    public string DisplayText => $"{Name}: {Transport.ToString().ToLowerInvariant()}://{Host}:{Port}";
}

/// <summary>References a simulator binary selected by a persisted profile.</summary>
/// <param name="Version">Version or user-provided version label.</param>
/// <param name="ExecutablePath">Absolute executable path.</param>
/// <param name="Source">Source identifier, such as external or verified cache.</param>
/// <param name="InstallationId">Stable discovered installation identity, when pinned.</param>
public sealed record SimulatorBinaryReference(
    string Version,
    string ExecutablePath,
    string Source,
    string? InstallationId = null);

/// <summary>Identifies a supported direct-SITL serial endpoint transport.</summary>
public enum ArduPilotSerialTransport
{
    /// <summary>SITL sends UDP datagrams to the configured endpoint.</summary>
    UdpClient,

    /// <summary>SITL establishes a TCP client connection to the configured endpoint.</summary>
    TcpClient
}

/// <summary>Describes one additional typed ArduPilot serial endpoint.</summary>
/// <param name="Index">ArduPilot serial index from 1 through 9; serial zero is reserved for MissionPlanner MAVLink.</param>
/// <param name="Transport">Endpoint transport.</param>
/// <param name="Host">Destination IP address or DNS host.</param>
/// <param name="Port">Destination port.</param>
public sealed record ArduPilotSerialEndpoint(
    int Index,
    ArduPilotSerialTransport Transport,
    string Host,
    int Port);

/// <summary>Configures typed ArduPilot SITL launch behavior.</summary>
/// <param name="Instance">Zero-based SITL instance number.</param>
/// <param name="SystemId">Expected MAVLink system ID.</param>
/// <param name="DefaultsFiles">Ordered default/parameter files passed as one typed value.</param>
/// <param name="WipeState">Whether the instance starts with wiped persistent state.</param>
/// <param name="ShowConsoleWindow">Whether a desktop process console may be shown.</param>
/// <param name="EnableMapIntegration">Whether MissionPlanner should present live map integration.</param>
/// <param name="AdditionalSerialEndpoints">Typed serial endpoints beyond MissionPlanner MAVLink on serial zero.</param>
public sealed record ArduPilotLaunchSettings(
    int Instance,
    byte SystemId,
    IReadOnlyList<string> DefaultsFiles,
    bool WipeState,
    bool ShowConsoleWindow,
    bool EnableMapIntegration,
    IReadOnlyList<ArduPilotSerialEndpoint>? AdditionalSerialEndpoints = null)
{
    /// <summary>Gets additional serial endpoints, including an empty fallback for older profiles.</summary>
    public IReadOnlyList<ArduPilotSerialEndpoint> EffectiveSerialEndpoints => AdditionalSerialEndpoints ?? [];

    /// <summary>Gets safe launch defaults for the first SITL instance.</summary>
    public static ArduPilotLaunchSettings Default { get; } = new(0, 1, [], false, false, true);
}

/// <summary>Defines a reproducible simulator launch profile.</summary>
/// <param name="Id">Stable profile identifier.</param>
/// <param name="Name">User-facing profile name.</param>
/// <param name="FirmwareFamily">Expected ArduPilot firmware family.</param>
/// <param name="FrameModel">Runtime frame or model identifier.</param>
/// <param name="Location">Initial location.</param>
/// <param name="Speedup">Simulation clock speed multiplier.</param>
/// <param name="Endpoints">Named connection endpoints.</param>
/// <param name="Binary">Selected simulator binary.</param>
/// <param name="AdditionalArguments">Additional argument tokens; never a shell command string.</param>
/// <param name="Environment">Runtime environment values.</param>
/// <param name="LaunchSettings">Typed ArduPilot-specific launch settings.</param>
public sealed record SimulatorProfile(
    Guid Id,
    string Name,
    FirmwareFamily FirmwareFamily,
    string FrameModel,
    SimulationLocation Location,
    double Speedup,
    IReadOnlyList<SimulationEndpoint> Endpoints,
    SimulatorBinaryReference Binary,
    IReadOnlyList<string> AdditionalArguments,
    IReadOnlyDictionary<string, string> Environment,
    ArduPilotLaunchSettings? LaunchSettings = null)
{
    /// <summary>Gets typed launch settings, including defaults for older persisted profiles.</summary>
    public ArduPilotLaunchSettings EffectiveLaunchSettings => LaunchSettings ?? ArduPilotLaunchSettings.Default;

    /// <summary>Creates a default local ArduCopter profile.</summary>
    /// <returns>A new profile with a unique identity.</returns>
    public static SimulatorProfile CreateDefault() => new(
        Guid.NewGuid(),
        "ArduCopter SITL",
        FirmwareFamily.ArduCopter,
        "quad",
        new SimulationLocation(-35.363261, 149.165230, 584, 353),
        1,
        [
            new SimulationEndpoint("MAVLink", SimulationEndpointTransport.Udp, "127.0.0.1", 14550),
            new SimulationEndpoint("Console", SimulationEndpointTransport.Tcp, "127.0.0.1", 5760)
        ],
        new SimulatorBinaryReference("unselected", string.Empty, "external"),
        [],
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
        ArduPilotLaunchSettings.Default);
}

/// <summary>Describes one profile or runtime validation problem.</summary>
/// <param name="Code">Stable diagnostic code.</param>
/// <param name="Path">Profile field or host resource.</param>
/// <param name="Message">User-facing explanation.</param>
public sealed record SimulationValidationIssue(string Code, string Path, string Message);

/// <summary>Uniquely identifies a runtime session owned by MissionPlanner.</summary>
/// <param name="RuntimeId">Adapter-provided exact runtime identity.</param>
/// <param name="Adapter">Runtime adapter name.</param>
/// <param name="ProcessId">Operating-system process identifier when applicable.</param>
public sealed record SimulatorRuntimeIdentity(string RuntimeId, string Adapter, int? ProcessId);

/// <summary>Contains one timestamped simulator output line.</summary>
/// <param name="Timestamp">Capture time.</param>
/// <param name="Stream">Output stream.</param>
/// <param name="Text">Line text.</param>
public sealed record SimulatorOutputLine(
    DateTimeOffset Timestamp,
    SimulatorOutputStream Stream,
    string Text);

/// <summary>Describes runtime termination.</summary>
/// <param name="ExitCode">Runtime exit code when available.</param>
/// <param name="WasExpected">Whether the runtime considered the exit expected.</param>
/// <param name="Message">Optional termination detail.</param>
public sealed record SimulatorRuntimeExit(int? ExitCode, bool WasExpected, string? Message);

/// <summary>Contains the immutable observable state of one simulation session.</summary>
/// <param name="SessionId">MissionPlanner-owned session identity.</param>
/// <param name="Profile">Profile used to start the session.</param>
/// <param name="State">Lifecycle state.</param>
/// <param name="RuntimeIdentity">Exact runtime identity after creation.</param>
/// <param name="ConnectionEndpoints">Endpoints reported by the runtime.</param>
/// <param name="StartedAt">Runtime start timestamp.</param>
/// <param name="EndedAt">Terminal timestamp.</param>
/// <param name="Message">Current user-facing status.</param>
/// <param name="Failure">Failure detail, when applicable.</param>
/// <param name="RecentOutput">Bounded recent output.</param>
/// <param name="VehicleId">Verified connected simulator vehicle identity.</param>
public sealed record SimulationSessionSnapshot(
    Guid SessionId,
    SimulatorProfile? Profile,
    SimulationSessionState State,
    SimulatorRuntimeIdentity? RuntimeIdentity,
    IReadOnlyList<SimulationEndpoint> ConnectionEndpoints,
    DateTimeOffset? StartedAt,
    DateTimeOffset? EndedAt,
    string Message,
    string? Failure,
    IReadOnlyList<SimulatorOutputLine> RecentOutput,
    VehicleId? VehicleId = null)
{
    /// <summary>Creates the initial stopped workspace state.</summary>
    public static SimulationSessionSnapshot Stopped { get; } = new(
        Guid.Empty,
        null,
        SimulationSessionState.Stopped,
        null,
        [],
        null,
        null,
        "No simulation is running.",
        null,
        []);
}

/// <summary>Provides simulation state-change event data.</summary>
/// <param name="snapshot">The new immutable state.</param>
public sealed class SimulationSessionChangedEventArgs(SimulationSessionSnapshot snapshot) : EventArgs
{
    /// <summary>Gets the new simulation state.</summary>
    public SimulationSessionSnapshot Snapshot { get; } = snapshot;
}
