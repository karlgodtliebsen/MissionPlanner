using MissionPlanner.Firmware.Model;

namespace MissionPlanner.Core.Firmware;

/// <summary>Blocks flashing when no safe platform/bootloader adapter has been installed.</summary>
public sealed class UnsupportedFirmwareFlashingService : IFirmwareFlashingService
{
    private const string Reason = "Firmware flashing is unavailable because no platform bootloader adapter is installed.";

    /// <inheritdoc />
    public FirmwareFlashSupport GetPlatformSupport(VehicleFirmwareIdentity identity)
    {
        return new FirmwareFlashSupport(false, Reason);
    }

    /// <inheritdoc />
    public FirmwareFlashSupport GetSupport(VehicleFirmwareIdentity identity, FirmwarePackage package)
    {
        return new FirmwareFlashSupport(false, Reason);
    }

    /// <inheritdoc />
    public Task<FirmwareFlashResult> FlashAsync(
        FirmwareFlashRequest request,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new FirmwareFlashResult(false, Reason));
    }
}
