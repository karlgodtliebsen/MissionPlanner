using MissionPlanner.Firmware.Model;

namespace MissionPlanner.Firmware.Presentation;

/// <summary>Identifies the presentation mode of the firmware page.</summary>
public enum FirmwarePageMode
{
    /// <summary>A supported ArduPilot vehicle is connected.</summary>
    Connected,
    /// <summary>No vehicle is connected and application firmware may be installed.</summary>
    Disconnected,
    /// <summary>A firmware operation owns the global operation boundary.</summary>
    OperationInProgress,
    /// <summary>Direct firmware installation is unavailable on the current platform.</summary>
    UnsupportedPlatform
}

/// <summary>Supplies presentation-neutral application state to the firmware page resolver.</summary>
/// <param name="IsDirectInstallationSupported">Whether direct serial firmware installation is supported.</param>
/// <param name="IsVehicleConnected">Whether an active vehicle connection exists.</param>
/// <param name="IsVehicleArmed">Whether the connected vehicle is armed.</param>
/// <param name="IsSupportedArduPilot">Whether the connection identifies a supported ArduPilot family.</param>
/// <param name="IsOperationInProgress">Whether a firmware operation currently owns the operation boundary.</param>
/// <param name="OperationState">The current operation stage, when an operation is active.</param>
public sealed record FirmwarePageContext(
    bool IsDirectInstallationSupported,
    bool IsVehicleConnected,
    bool IsVehicleArmed,
    bool IsSupportedArduPilot,
    bool IsOperationInProgress,
    FirmwareOperationState? OperationState = null);

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

/// <summary>Resolves firmware-page mode and capabilities from application state.</summary>
public interface IFirmwarePageModeResolver
{
    /// <summary>Resolves the complete page policy for <paramref name="context"/>.</summary>
    FirmwarePageState Resolve(FirmwarePageContext context);
}
