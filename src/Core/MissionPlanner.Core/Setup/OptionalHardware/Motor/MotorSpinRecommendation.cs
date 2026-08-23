namespace MissionPlanner.Core.Setup.OptionalHardware.Motor;

/// <summary>Represents a validated motor-spin recommendation.</summary>
/// <param name="Success">Whether the recommendation is safe and available.</param>
/// <param name="ParameterName">The target parameter name.</param>
/// <param name="Percent">The recommended percentage when successful.</param>
/// <param name="NormalizedValue">The recommended normalized value when successful.</param>
/// <param name="Message">A user-facing recommendation or validation message.</param>
public sealed record MotorSpinRecommendation(
    bool Success,
    string ParameterName,
    double? Percent,
    float? NormalizedValue,
    string Message);
