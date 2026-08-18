namespace MissionPlanner.Core.Setup.MandatoryHardware;

/// <summary>Represents the outcome of a confirmed compass parameter write.</summary>
/// <param name="Success">Whether the vehicle confirmed the new value by readback.</param>
/// <param name="Message">A user-facing explanation of the outcome.</param>
public sealed record CompassParameterApplyResult(bool Success, string Message);
