using MissionPlanner.Firmware.Model;

namespace MissionPlanner.Firmware.Presentation;

/// <summary>Describes all firmware-page capabilities for one application-state snapshot.</summary>
/// <param name="Mode">The resolved page mode.</param>
/// <param name="ShowConnectionWarning">Whether to explain that application installation requires disconnecting.</param>
/// <param name="ShowCatalogue">Whether to display normal firmware choices.</param>
/// <param name="ShowReleaseChannels">Whether to display Stable, Beta, and Latest selection.</param>
/// <param name="ShowAllOptions">Whether to display the complete catalogue action.</param>
/// <param name="ShowCustomFirmware">Whether to display custom firmware selection.</param>
/// <param name="ShowDeviceStatus">Whether to display direct-device discovery status.</param>
/// <param name="CanInstallApplicationFirmware">Whether any normal application install command may execute.</param>
/// <param name="CanUpdateEmbeddedBootloader">Whether the connected bootloader update command may execute.</param>
/// <param name="ShowProgress">Whether operation progress should replace normal page actions.</param>
/// <param name="CanNavigateAway">Whether navigation may leave the firmware page safely.</param>
/// <param name="OperationState">The current operation stage, when present.</param>
public sealed record FirmwarePageState(
    FirmwarePageMode Mode,
    bool ShowConnectionWarning,
    bool ShowCatalogue,
    bool ShowReleaseChannels,
    bool ShowAllOptions,
    bool ShowCustomFirmware,
    bool ShowDeviceStatus,
    bool CanInstallApplicationFirmware,
    bool CanUpdateEmbeddedBootloader,
    bool ShowProgress,
    bool CanNavigateAway,
    FirmwareOperationState? OperationState);
