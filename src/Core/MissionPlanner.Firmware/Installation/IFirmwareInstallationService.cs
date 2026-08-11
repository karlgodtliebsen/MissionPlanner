using MissionPlanner.Firmware.Model;

namespace MissionPlanner.Firmware.Installation;

/// <summary>Runs the disconnected application-firmware workflow.</summary>
public interface IFirmwareInstallationService
{
    /// <summary>Installs firmware only after validation, identification, compatibility, and confirmation.</summary>
    Task<FirmwareOperationResult> InstallAsync(
        FirmwareInstallationRequest request,
        IProgress<FirmwareProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
