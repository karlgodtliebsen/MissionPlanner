namespace MissionPlanner.Core.Setup.MandatoryHardware;

/// <summary>Projects one physical servo output channel with configuration and live PWM.</summary>
/// <param name="ChannelNumber">The one-based physical servo output number.</param>
/// <param name="FunctionValue">The configured output function value.</param>
/// <param name="FunctionName">The human-readable function name.</param>
/// <param name="Reversed">Whether the physical output is reversed.</param>
/// <param name="MinimumPwm">The configured minimum PWM.</param>
/// <param name="TrimPwm">The configured trim PWM.</param>
/// <param name="MaximumPwm">The configured maximum PWM.</param>
/// <param name="LivePwm">The latest raw output PWM, when reported.</param>
/// <param name="IsStale">Whether the live output telemetry is stale.</param>
/// <param name="AllowedMinimumPwm">The lowest value allowed by metadata or the established servo fallback.</param>
/// <param name="AllowedMaximumPwm">The highest value allowed by metadata or the established servo fallback.</param>
public sealed record ServoOutputInfo(
    int ChannelNumber,
    int FunctionValue,
    string FunctionName,
    bool Reversed,
    int MinimumPwm,
    int TrimPwm,
    int MaximumPwm,
    int? LivePwm,
    bool IsStale,
    int AllowedMinimumPwm,
    int AllowedMaximumPwm)
{
    /// <summary>Gets the one-based physical output number for compatibility with existing consumers.</summary>
    public int Output => ChannelNumber;
}
