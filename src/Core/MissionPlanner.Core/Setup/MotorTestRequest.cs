using MissionPlanner.Core.Vehicles.Models;

namespace MissionPlanner.Core.Setup;

/// <summary>Describes a bounded motor-test request.</summary>
/// <param name="MotorIndex">The one-based motor index.</param>
/// <param name="ThrottleType">How the throttle value is expressed.</param>
/// <param name="ThrottleValue">The throttle value in percent or PWM.</param>
/// <param name="DurationSeconds">The bounded run duration in seconds.</param>
public sealed record MotorTestRequest(int MotorIndex, MotorThrottleType ThrottleType, double ThrottleValue, double DurationSeconds);
