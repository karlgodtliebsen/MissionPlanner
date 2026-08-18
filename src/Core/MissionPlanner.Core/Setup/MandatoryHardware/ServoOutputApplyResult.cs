namespace MissionPlanner.Core.Setup.MandatoryHardware;

/// <summary>Represents the outcome of a confirmed servo function write.</summary>
/// <param name="Success">Whether the vehicle confirmed the new function by readback.</param>
/// <param name="Message">A user-facing explanation of the outcome.</param>
public sealed record ServoOutputApplyResult(bool Success, string Message);
