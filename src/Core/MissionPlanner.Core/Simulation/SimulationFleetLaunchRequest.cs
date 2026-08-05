using MissionPlanner.Simulation;

namespace MissionPlanner.Core.Simulation;

/// <summary>Requests deterministic allocation and launch of multiple SITL instances.</summary>
/// <param name="BaseProfile">Base simulator profile.</param>
/// <param name="Count">Number of instances.</param>
/// <param name="Formation">Ordered launch offsets.</param>
/// <param name="PortStride">Port increment per instance.</param>
/// <param name="MaximumConcurrency">Maximum concurrent start or stop operations.</param>
public sealed record SimulationFleetLaunchRequest(
    SimulatorProfile BaseProfile,
    int Count,
    SimulationFormationProfile Formation,
    int PortStride = 10,
    int MaximumConcurrency = 3);
