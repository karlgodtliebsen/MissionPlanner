namespace MissionPlanner.Firmware.Installation;

/// <summary>Repeats the final pre-erase compatibility evidence.</summary>
public sealed record FirmwareInstallationConfirmation(
    int FirmwareBoardId,
    int DetectedBoardId,
    int BootloaderRevision,
    long ImageSize,
    string Source,
    bool BoardIdMismatchOverrideUsed = false,
    string? RequiredPhrase = null);
