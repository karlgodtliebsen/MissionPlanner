namespace MissionPlanner.Core.Setup.OptionalHardware.Motor;

/// <summary>Represents current motor-spin parameter availability and normalized values.</summary>
/// <param name="SpinArmNormalized">The current MOT_SPIN_ARM value, or null when unavailable.</param>
/// <param name="SpinMinNormalized">The current MOT_SPIN_MIN value, or null when unavailable.</param>
public sealed record MotorSpinParameterState(float? SpinArmNormalized, float? SpinMinNormalized)
{
    /// <summary>Gets whether MOT_SPIN_ARM is available.</summary>
    public bool HasSpinArm => SpinArmNormalized.HasValue;

    /// <summary>Gets whether MOT_SPIN_MIN is available.</summary>
    public bool HasSpinMin => SpinMinNormalized.HasValue;

    /// <summary>Gets the current MOT_SPIN_ARM percentage.</summary>
    public double? SpinArmPercent => SpinArmNormalized is { } value ? MotorSpinPercentage.ToPercent(value) : null;

    /// <summary>Gets the current MOT_SPIN_MIN percentage.</summary>
    public double? SpinMinPercent => SpinMinNormalized is { } value ? MotorSpinPercentage.ToPercent(value) : null;
}
