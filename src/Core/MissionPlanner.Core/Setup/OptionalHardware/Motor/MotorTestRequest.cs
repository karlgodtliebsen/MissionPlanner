namespace MissionPlanner.Core.Setup.OptionalHardware.Motor;

/// <summary>Describes a bounded motor-test request.</summary>
/// <param name="TestOrder">The one-based motor-test order (A = 1, B = 2, and so on).</param>
/// <param name="ThrottleType">How the throttle value is expressed.</param>
/// <param name="ThrottleValue">The throttle value in percent or PWM.</param>
/// <param name="DurationSeconds">The bounded run duration in seconds.</param>
public sealed record MotorTestRequest(int TestOrder, MotorThrottleType ThrottleType, double ThrottleValue, double DurationSeconds);
