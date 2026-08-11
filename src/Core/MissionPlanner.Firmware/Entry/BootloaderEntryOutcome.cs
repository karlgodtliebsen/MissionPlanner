namespace MissionPlanner.Firmware.Entry;

/// <summary>Identifies the result of one bootloader-entry attempt.</summary>
public enum BootloaderEntryOutcome
{
    /// <summary>The strategy does not apply to the current context.</summary>
    NotApplicable,

    /// <summary>The strategy completed and discovery may continue.</summary>
    ContinueDiscovery,

    /// <summary>A bootloader was directly identified.</summary>
    BootloaderIdentified,

    /// <summary>The strategy failed and another strategy may be attempted.</summary>
    Failed
}
