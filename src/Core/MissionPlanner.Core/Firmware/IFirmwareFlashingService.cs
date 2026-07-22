using MissionPlanner.Core.Vehicles.Models;

namespace MissionPlanner.Core.Firmware;

/// <summary>Abstracts platform- and bootloader-specific firmware flashing.</summary>
public interface IFirmwareFlashingService
{
    /// <summary>Evaluates whether any platform/bootloader adapter supports the reported board.</summary>
    /// <param name="identity">The protocol-reported board identity.</param>
    /// <returns>The platform support result.</returns>
    FirmwareFlashSupport GetPlatformSupport(VehicleFirmwareIdentity identity);

    /// <summary>Evaluates adapter support using technical board identity and a verified package.</summary>
    /// <param name="identity">The protocol-reported board identity.</param>
    /// <param name="package">The verified package.</param>
    /// <returns>The support result.</returns>
    FirmwareFlashSupport GetSupport(VehicleFirmwareIdentity identity, FirmwarePackage package);

    /// <summary>Flashes a verified package after normal MAVLink has disconnected.</summary>
    /// <param name="request">The verified flash request.</param>
    /// <param name="progress">Optional progress from zero to one.</param>
    /// <param name="cancellationToken">A token that cancels flashing when the adapter can do so safely.</param>
    /// <returns>The flashing result.</returns>
    Task<FirmwareFlashResult> FlashAsync(
        FirmwareFlashRequest request,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default);
}
