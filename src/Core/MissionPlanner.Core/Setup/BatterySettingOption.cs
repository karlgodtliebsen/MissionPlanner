namespace MissionPlanner.Core.Setup;

/// <summary>Represents one selectable enumerated value for a battery setting.</summary>
/// <param name="Value">The stored numeric value.</param>
/// <param name="Name">The human-readable label.</param>
public sealed record BatterySettingOption(double Value, string Name);
