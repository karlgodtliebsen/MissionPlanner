using MissionPlanner.Firmware.Model;

namespace MissionPlanner.Firmware.Installation;

/// <summary>Repeats the final pre-erase compatibility evidence.</summary>
public sealed record FirmwareInstallationConfirmation(
    int FirmwareBoardId,
    int BootloaderBoardId,
    int BootloaderRevision,
    long ImageSize,
    string Source,
    bool BoardIdMismatchOverrideUsed = false,
    string? RequiredPhrase = null)
{
    /// <summary>Gets the explicit operation intent.</summary>
    public FirmwareInstallMode Mode { get; init; }
    /// <summary>Gets source-attributed compatibility evidence.</summary>
    public FirmwareCompatibilityResult? IdentityDecision { get; init; }
}
