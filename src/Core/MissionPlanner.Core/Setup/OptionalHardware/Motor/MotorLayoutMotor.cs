namespace MissionPlanner.Core.Setup.OptionalHardware.Motor;

/// <summary>One frame-derived motor test position.</summary>
/// <param name="MotorNumber">The ArduPilot logical motor number.</param>
/// <param name="TestOrder">The one-based order used by the motor-test command.</param>
/// <param name="Rotation">The configured direction of rotation.</param>
/// <param name="Roll">The motor's roll factor in the frame definition.</param>
/// <param name="Pitch">The motor's pitch factor in the frame definition.</param>
public sealed record MotorLayoutMotor(
    int MotorNumber,
    int TestOrder,
    MotorRotation Rotation,
    double Roll,
    double Pitch)
{
    /// <summary>Gets the user-facing motor-test label.</summary>
    public string Label => $"Test {(char)('A' + TestOrder - 1)} — Motor {MotorNumber} — {RotationLabel}";

    private string RotationLabel => Rotation switch
    {
        MotorRotation.Clockwise => "CW",
        MotorRotation.CounterClockwise => "CCW",
        var _ => "Unknown"
    };
}
