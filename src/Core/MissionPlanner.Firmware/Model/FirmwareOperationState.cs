namespace MissionPlanner.Firmware.Model;

/// <summary>Identifies a firmware operation lifecycle state.</summary>
public enum FirmwareOperationState
{
    /// <summary>No operation is active.</summary>
    Idle,

    /// <summary>The release catalogue is loading.</summary>
    LoadingCatalog,

    /// <summary>A release is being selected.</summary>
    SelectingFirmware,

    /// <summary>An artifact is downloading.</summary>
    Downloading,

    /// <summary>A package is being validated.</summary>
    ValidatingPackage,

    /// <summary>The workflow is waiting for a matching device.</summary>
    WaitingForDevice,

    /// <summary>The device is being asked to enter its bootloader.</summary>
    EnteringBootloader,

    /// <summary>The bootloader identity is being queried.</summary>
    IdentifyingBootloader,

    /// <summary>Firmware and hardware compatibility is being checked.</summary>
    CheckingCompatibility,

    /// <summary>Application flash is being erased.</summary>
    Erasing,

    /// <summary>Application flash is being programmed.</summary>
    Programming,

    /// <summary>Programmed bytes are being verified.</summary>
    Verifying,

    /// <summary>The device is rebooting.</summary>
    Rebooting,

    /// <summary>The workflow is waiting for application firmware to appear.</summary>
    WaitingForApplication,

    /// <summary>The operation completed successfully.</summary>
    Completed,

    /// <summary>The operation stopped safely before completion.</summary>
    Cancelled,

    /// <summary>The operation failed.</summary>
    Failed,

    /// <summary>Probing the selected device for an existing ArduPilot bootloader.</summary>
    CheckingForBootloader,

    /// <summary>Requesting ArduPilot bootloader reboot through temporary MAVLink access.</summary>
    RequestingBootloaderReboot,

    /// <summary>Waiting for the selected physical controller's ArduPilot bootloader.</summary>
    WaitingForBootloader,

    /// <summary>Automatic entry failed and an operator reset or reconnect is required.</summary>
    ManualBootloaderReconnectRequired
}
