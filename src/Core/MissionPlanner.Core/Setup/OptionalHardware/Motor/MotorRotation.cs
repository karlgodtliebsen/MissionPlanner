namespace MissionPlanner.Core.Setup.OptionalHardware.Motor;

/// <summary>Describes the configured direction of motor rotation.</summary>
public enum MotorRotation
{
    /// <summary>The frame definition does not specify a direction.</summary>
    Unknown,

    /// <summary>Clockwise rotation.</summary>
    Clockwise,

    /// <summary>Counter-clockwise rotation.</summary>
    CounterClockwise
}
