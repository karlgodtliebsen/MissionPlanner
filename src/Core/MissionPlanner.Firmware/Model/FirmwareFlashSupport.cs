namespace MissionPlanner.Firmware.Model;

/// <summary>Describes whether a platform adapter can flash a selected package.</summary>
/// <param name="IsSupported">Whether flashing is supported.</param>
/// <param name="Reason">A user-facing support or blocking explanation.</param>
public sealed record FirmwareFlashSupport(bool IsSupported, string Reason);
