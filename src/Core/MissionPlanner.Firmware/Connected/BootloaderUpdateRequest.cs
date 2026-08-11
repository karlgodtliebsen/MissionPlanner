namespace MissionPlanner.Firmware.Connected;

/// <summary>Defines a connected embedded bootloader update request.</summary>
public sealed record BootloaderUpdateRequest(bool WarningAccepted);
