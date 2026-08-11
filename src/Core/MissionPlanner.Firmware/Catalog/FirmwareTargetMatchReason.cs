namespace MissionPlanner.Firmware.Catalog;

/// <summary>Identifies why a firmware target is recommended.</summary>
public enum FirmwareTargetMatchReason
{
    /// <summary>No hardware evidence supports automatic selection.</summary>
    ManualSelection,

    /// <summary>The target matches a previously selected board.</summary>
    PreviouslySelectedTarget,

    /// <summary>A detected product or board hint matches a bootloader alias.</summary>
    ExactBootloaderAliasMatch,

    /// <summary>A detected USB VID/PID matches the manifest target.</summary>
    ExactUsbMatch
}
