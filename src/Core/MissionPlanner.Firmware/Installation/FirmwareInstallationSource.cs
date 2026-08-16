namespace MissionPlanner.Firmware.Installation;

/// <summary>Identifies the provenance of an application firmware installation.</summary>
public enum FirmwareInstallationSource
{
    /// <summary>Firmware selected from and validated against the official catalogue.</summary>
    OfficialCatalogue,

    /// <summary>Firmware explicitly selected from a local file by the operator.</summary>
    LocalCustom
}
