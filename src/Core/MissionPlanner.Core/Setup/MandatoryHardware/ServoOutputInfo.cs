namespace MissionPlanner.Core.Setup.MandatoryHardware;

/// <summary>Projects one servo output channel with its function and live PWM.</summary>
/// <param name="Output">The one-based servo output number.</param>
/// <param name="FunctionValue">The configured output function value.</param>
/// <param name="FunctionName">The human-readable function name.</param>
/// <param name="LivePwm">The latest raw output PWM, when reported.</param>
/// <param name="IsStale">Whether the live output telemetry is stale.</param>
public sealed record ServoOutputInfo(
    int Output,
    int FunctionValue,
    string FunctionName,
    int? LivePwm,
    bool IsStale);
