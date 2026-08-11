namespace MissionPlanner.Firmware.Installation;

/// <summary>Defines a non-dialog manual action request.</summary>
public sealed record FirmwareManualAction(string Code, string? TechnicalDetail = null);
