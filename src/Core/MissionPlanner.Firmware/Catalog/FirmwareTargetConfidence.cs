namespace MissionPlanner.Firmware.Catalog;

/// <summary>Describes the strength of target-selection evidence.</summary>
public enum FirmwareTargetConfidence
{
    /// <summary>No device evidence is available.</summary>
    Low,

    /// <summary>USB/product hints or remembered user intent support a candidate, not an exact board.</summary>
    Medium,

    /// <summary>Protocol-proven board identity supports the target.</summary>
    High
}
