using MissionPlanner.Firmware.Model;

namespace MissionPlanner.Firmware.Presentation;

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
