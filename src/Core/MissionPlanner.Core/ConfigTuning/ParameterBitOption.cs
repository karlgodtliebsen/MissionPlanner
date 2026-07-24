namespace MissionPlanner.Core.ConfigTuning;

/// <summary>Represents one bit of a bitmask parameter.</summary>
/// <param name="Bit">The zero-based bit index.</param>
/// <param name="Label">The human-readable label.</param>
public sealed record ParameterBitOption(int Bit, string Label);
