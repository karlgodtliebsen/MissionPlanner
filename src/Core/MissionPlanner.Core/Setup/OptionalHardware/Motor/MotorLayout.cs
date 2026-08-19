namespace MissionPlanner.Core.Setup.OptionalHardware.Motor;

/// <summary>A safely resolved matrix-motor layout.</summary>
public sealed record MotorLayout(int FrameClass, int FrameType, string DisplayName, IReadOnlyList<MotorLayoutMotor> Motors);
