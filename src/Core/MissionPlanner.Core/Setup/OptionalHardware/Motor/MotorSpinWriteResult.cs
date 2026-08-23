namespace MissionPlanner.Core.Setup.OptionalHardware.Motor;

/// <summary>Reports a confirmed or failed motor-spin parameter write.</summary>
/// <param name="Success">Whether vehicle readback confirmed the write.</param>
/// <param name="Message">A user-facing result message.</param>
public sealed record MotorSpinWriteResult(bool Success, string Message);
