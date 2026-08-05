namespace MissionPlanner.Simulation;

/// <summary>Describes a named launch-location preset.</summary>
/// <param name="Key">Stable preset key.</param>
/// <param name="Name">User-facing name.</param>
/// <param name="Location">Typed start location.</param>
public sealed record SimulationLocationPreset(
    string Key,
    string Name,
    SimulationLocation Location);
