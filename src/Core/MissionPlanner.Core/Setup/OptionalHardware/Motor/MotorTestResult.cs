namespace MissionPlanner.Core.Setup.OptionalHardware.Motor;

/// <summary>Represents the outcome of a motor-test request.</summary>
/// <param name="Success">Whether the vehicle accepted the bounded test.</param>
/// <param name="Message">A user-facing explanation of the outcome.</param>
public sealed record MotorTestResult(bool Success, string Message);
