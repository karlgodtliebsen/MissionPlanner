namespace MissionPlanner.Core.Setup;

/// <summary>Represents the outcome of a confirmed radio endpoint write.</summary>
/// <param name="Success">Whether all endpoints were confirmed by readback.</param>
/// <param name="Message">A user-facing explanation of the outcome.</param>
public sealed record RadioWriteResult(bool Success, string Message);
