namespace MissionPlanner.Simulation;

/// <summary>Stores one requested control value in a reusable preset.</summary>
/// <param name="ControlKey">Logical control key.</param>
/// <param name="Value">Requested value.</param>
/// <param name="Duration">Bounded duration for hazardous controls.</param>
public sealed record SimulationPresetControlValue(
    string ControlKey,
    double Value,
    TimeSpan? Duration);
