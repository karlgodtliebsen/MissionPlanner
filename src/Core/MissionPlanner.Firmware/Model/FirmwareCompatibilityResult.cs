namespace MissionPlanner.Firmware.Model;

/// <summary>Represents a compatibility decision with a stable reason code.</summary>
public sealed record FirmwareCompatibilityResult(bool IsCompatible, string Code, string? TechnicalDetail = null);
