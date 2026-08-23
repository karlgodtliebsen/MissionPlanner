namespace MissionPlanner.Core.Setup.MandatoryHardware;

/// <summary>Contains editable settings for one physical servo output.</summary>
/// <param name="ChannelNumber">The one-based physical output channel.</param>
/// <param name="Reversed">Whether output direction is reversed.</param>
/// <param name="FunctionValue">The configured ArduPilot output function.</param>
/// <param name="MinimumPwm">The minimum output PWM.</param>
/// <param name="TrimPwm">The trim output PWM.</param>
/// <param name="MaximumPwm">The maximum output PWM.</param>
public sealed record ServoOutputSettings(
    int ChannelNumber,
    bool Reversed,
    int FunctionValue,
    int MinimumPwm,
    int TrimPwm,
    int MaximumPwm);
