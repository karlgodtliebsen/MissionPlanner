namespace MissionPlanner.Firmware.Model;

/// <summary>Identifies the firmware use case being executed.</summary>
public enum FirmwareOperationKind
{
    /// <summary>Installs application firmware through an external bootloader.</summary>
    InstallApplicationFirmware,

    /// <summary>Installs application firmware and its bootloader through STM32 USB DFU.</summary>
    InstallApplicationAndBootloaderDfu,

    /// <summary>Updates an embedded bootloader through a connected application.</summary>
    UpdateEmbeddedBootloader,

    /// <summary>Owns serial devices for non-destructive firmware identification.</summary>
    ProbeFirmwareIdentity,

    /// <summary>Resolves and inspects a DFU artifact without opening a programming device.</summary>
    PrepareDfuArtifact
}
