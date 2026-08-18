namespace MissionPlanner.Core.Setup.MandatoryHardware;

/// <summary>Represents the outcome of a confirmed flight-mode slot write.</summary>
/// <param name="Success">Whether the vehicle confirmed the new mode by readback.</param>
/// <param name="Message">A user-facing explanation of the outcome.</param>
public sealed record FlightModeApplyResult(bool Success, string Message);
