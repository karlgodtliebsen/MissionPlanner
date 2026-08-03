using MissionPlanner.Firmware.Model;

namespace MissionPlanner.Firmware.Compatibility;

/// <summary>Provides fail-closed compatibility decisions with stable diagnostic codes.</summary>
public sealed class FirmwareCompatibilityService : IFirmwareCompatibilityService
{
    /// <inheritdoc />
    public FirmwareCompatibilityResult Check(ApjFirmwarePackage firmware, BootloaderIdentity bootloader)
    {
        ArgumentNullException.ThrowIfNull(firmware);
        ArgumentNullException.ThrowIfNull(bootloader);
        if (firmware.BoardId != bootloader.BoardId && !(bootloader.BoardId == 33 && firmware.BoardId == 9))
            return Blocked("compatibility.board-id-mismatch", $"Firmware board ID: {firmware.BoardId}; Detected board ID: {bootloader.BoardId}");
        if (firmware.BoardRevision > 0 && bootloader.BoardRevision < firmware.BoardRevision)
            return Blocked("compatibility.board-revision-too-old", $"Required board revision: {firmware.BoardRevision}; Detected board revision: {bootloader.BoardRevision}");
        if (firmware.BoardRevisionMaximum is { } maximum && bootloader.BoardRevision > maximum)
            return Blocked("compatibility.board-revision-too-new", $"Maximum board revision: {maximum}; Detected board revision: {bootloader.BoardRevision}");
        if (firmware.Image.Length > bootloader.FlashSize)
            return Blocked("compatibility.internal-image-too-large", $"Firmware image bytes: {firmware.Image.Length}; Available bytes: {bootloader.FlashSize}");
        if (firmware.ExternalImage.Length > bootloader.ExternalFlashSize)
            return Blocked("compatibility.external-flash-insufficient", $"External image bytes: {firmware.ExternalImage.Length}; Available bytes: {bootloader.ExternalFlashSize}");
        if (firmware.MinimumBootloaderRevision > bootloader.BootloaderRevision)
            return Blocked("compatibility.bootloader-too-old", $"Required bootloader revision: {firmware.MinimumBootloaderRevision}; Detected revision: {bootloader.BootloaderRevision}");
        if (firmware.RequiresSecureBoot == true && bootloader.IsSecure != true)
            return Blocked("compatibility.secure-boot-required", $"Firmware requires secure boot; Detected secure state: {bootloader.IsSecure?.ToString() ?? "unknown"}");
        if (bootloader.IsSecure == true && firmware.IsSigned != true)
            return Blocked("compatibility.signed-image-required", $"Secure bootloader requires a signed image; Package signed state: {firmware.IsSigned?.ToString() ?? "unknown"}");
        return new FirmwareCompatibilityResult(true, "compatibility.compatible");
    }

    private static FirmwareCompatibilityResult Blocked(string code, string detail) => new(false, code, detail);
}
