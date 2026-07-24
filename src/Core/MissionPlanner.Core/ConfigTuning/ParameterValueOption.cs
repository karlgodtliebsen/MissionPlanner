namespace MissionPlanner.Core.ConfigTuning;

/// <summary>Represents a selectable enumerated parameter value.</summary>
/// <param name="Value">The stored numeric value.</param>
/// <param name="Label">The human-readable label.</param>
public sealed record ParameterValueOption(double Value, string Label);
