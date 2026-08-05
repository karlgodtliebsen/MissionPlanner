using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Simulation;

namespace MissionPlanner.Core.Simulation;

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
    public static SimulatorProfile CreateDefault()
    {
        return new SimulatorProfile(
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
}
