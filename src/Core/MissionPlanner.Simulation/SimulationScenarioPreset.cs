namespace MissionPlanner.Simulation;

/// <summary>Defines a reusable environment/fault preset separate from a launch profile.</summary>
/// <param name="Id">Stable preset identity.</param>
/// <param name="Name">User-facing preset name.</param>
/// <param name="Location">Optional launch location.</param>
/// <param name="Controls">Requested environment and fault values.</param>
public sealed record SimulationScenarioPreset(
    Guid Id,
    string Name,
    SimulationLocation? Location,
    IReadOnlyList<SimulationPresetControlValue> Controls);
