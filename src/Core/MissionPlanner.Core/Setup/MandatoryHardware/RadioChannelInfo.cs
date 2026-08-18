namespace MissionPlanner.Core.Setup.MandatoryHardware;

/// <summary>Projects the live state of one RC input channel.</summary>
/// <param name="Number">The one-based channel number.</param>
/// <param name="Pwm">The latest raw PWM value in microseconds.</param>
/// <param name="Normalized">The trim-centered normalized position from minus one to one.</param>
/// <param name="Minimum">The configured minimum endpoint.</param>
/// <param name="Maximum">The configured maximum endpoint.</param>
/// <param name="Trim">The configured trim (center) value.</param>
/// <param name="Reversed">Whether the channel is reversed.</param>
/// <param name="FunctionName">The mapped pilot function, when known.</param>
/// <param name="DeadZone">The configured dead zone for a centered pilot axis.</param>
/// <param name="Kind">The operational channel presentation.</param>
public sealed record RadioChannelInfo(
    int Number,
    int Pwm,
    double Normalized,
    int Minimum,
    int Maximum,
    int Trim,
    bool Reversed,
    string? FunctionName,
    int DeadZone = 0,
    RadioChannelKind Kind = RadioChannelKind.Auxiliary);
