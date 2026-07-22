using MissionPlanner.Core.Vehicles.Models;

namespace MissionPlanner.Core.Firmware;

/// <summary>Blocks flashing when no safe platform/bootloader adapter has been installed.</summary>
public sealed class UnsupportedFirmwareFlashingService : IFirmwareFlashingService
{
    private const string Reason = "Firmware flashing is unavailable because no platform bootloader adapter is installed.";

    /// <inheritdoc />
    public FirmwareFlashSupport GetPlatformSupport(VehicleFirmwareIdentity identity) => new(false, Reason);

    /// <inheritdoc />
    public FirmwareFlashSupport GetSupport(VehicleFirmwareIdentity identity, FirmwarePackage package) => new(false, Reason);

    /// <inheritdoc />
    public Task<FirmwareFlashResult> FlashAsync(
        FirmwareFlashRequest request,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new FirmwareFlashResult(false, Reason));
}
