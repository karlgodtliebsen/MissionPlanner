namespace MissionPlanner.Core.Setup.OptionalHardware;

/// <summary>Identifies how a motor-test throttle value is expressed.</summary>
public enum MotorThrottleType
{
    /// <summary>Throttle as a percentage from zero to one hundred.</summary>
    Percent,

    /// <summary>Throttle as an absolute PWM value in microseconds.</summary>
    Pwm
}
