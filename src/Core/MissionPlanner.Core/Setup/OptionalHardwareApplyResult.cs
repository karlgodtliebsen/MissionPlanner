namespace MissionPlanner.Core.Setup;

/// <summary>Represents the outcome of a confirmed peripheral setting write.</summary>
/// <param name="Success">Whether the vehicle confirmed the new value by readback.</param>
/// <param name="Message">A user-facing explanation of the outcome.</param>
/// <param name="RequiresReboot">Whether the confirmed change requires a reboot.</param>
public sealed record OptionalHardwareApplyResult(bool Success, string Message, bool RequiresReboot = false);
