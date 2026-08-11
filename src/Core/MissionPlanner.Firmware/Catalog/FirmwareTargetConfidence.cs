namespace MissionPlanner.Firmware.Catalog;

/// <summary>Describes the strength of target-selection evidence.</summary>
public enum FirmwareTargetConfidence
{
    /// <summary>No device evidence is available.</summary>
    Low,

    /// <summary>Only remembered user intent supports the target.</summary>
    Medium,

    /// <summary>Current device evidence supports the target.</summary>
    High
}
