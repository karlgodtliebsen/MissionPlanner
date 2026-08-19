namespace MissionPlanner.Core.Setup.OptionalHardware.Motor;

/// <summary>One frame-derived motor test position.</summary>
public sealed record MotorLayoutMotor(int MotorNumber, int TestOrder, string Label);
