namespace MissionPlanner.Core.Simulation;

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
