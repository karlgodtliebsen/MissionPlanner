namespace MissionPlanner.Firmware.Model;

/// <summary>Identifies a firmware release stream.</summary>
public enum FirmwareReleaseChannel
{
    /// <summary>A tested production release.</summary>
    Stable,
    /// <summary>A prerelease candidate.</summary>
    Beta,
    /// <summary>The latest development build.</summary>
    Latest,
    /// <summary>An archived release.</summary>
    Historical,
    /// <summary>A user-supplied image.</summary>
    Custom
}

/// <summary>Identifies the vehicle family supported by firmware.</summary>
public enum FirmwareVehicleType
{
    /// <summary>A multicopter.</summary>
    Copter,
    /// <summary>A helicopter.</summary>
    Helicopter,
    /// <summary>A fixed-wing aircraft.</summary>
    Plane,
    /// <summary>A ground or surface vehicle.</summary>
    Rover,
    /// <summary>An underwater vehicle.</summary>
    Sub,
    /// <summary>An antenna tracker.</summary>
    AntennaTracker,
    /// <summary>An airship.</summary>
    Blimp,
    /// <summary>An unrecognized vehicle family.</summary>
    Unknown
}

/// <summary>Identifies a firmware package format.</summary>
public enum FirmwareImageFormat
{
    /// <summary>An ArduPilot JSON package.</summary>
    Apj,
    /// <summary>A PX4 JSON package.</summary>
    Px4,
    /// <summary>An Intel HEX image.</summary>
    IntelHex,
    /// <summary>An ArduPilot binary container.</summary>
    Abin,
    /// <summary>An unrecognized format.</summary>
    Unknown
}

/// <summary>Identifies the firmware use case being executed.</summary>
public enum FirmwareOperationKind
{
    /// <summary>Installs application firmware through an external bootloader.</summary>
    InstallApplicationFirmware,
    /// <summary>Updates an embedded bootloader through a connected application.</summary>
    UpdateEmbeddedBootloader
}

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
    Failed
}
