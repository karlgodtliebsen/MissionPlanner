namespace MissionPlanner.Core.Setup;

/// <summary>Represents one selectable compass board-orientation option.</summary>
/// <param name="Value">The MAV_SENSOR_ORIENTATION enumeration value.</param>
/// <param name="Name">The human-readable orientation label.</param>
public sealed record CompassOrientationOption(int Value, string Name);
