namespace MissionPlanner.Firmware.Dfu;

/// <summary>Identifies the DFU installation lifecycle.</summary>
public enum DfuOperationState
{
    /// <summary>No DFU operation is active.</summary>
    Idle,

    /// <summary>The external provider is being located.</summary>
    LocatingTool,

    /// <summary>The workflow is waiting for a DFU USB device.</summary>
    WaitingForDevice,

    /// <summary>The provider is inspecting MCU and driver evidence.</summary>
    InspectingDevice,

    /// <summary>An official or local artifact is being resolved.</summary>
    ResolvingArtifact,

    /// <summary>The resolved artifact is downloading.</summary>
    DownloadingArtifact,

    /// <summary>Intel HEX structure and address policy are being inspected.</summary>
    InspectingHex,

    /// <summary>The workflow is waiting for explicit target confirmation.</summary>
    AwaitingConfirmation,

    /// <summary>The provider is erasing or writing flash.</summary>
    Programming,

    /// <summary>The provider is verifying programmed flash.</summary>
    Verifying,

    /// <summary>The provider is requesting detach, reset, or start.</summary>
    Detaching,

    /// <summary>The workflow is waiting for application firmware to enumerate.</summary>
    WaitingForApplication,

    /// <summary>Programming and required verification completed.</summary>
    Completed,

    /// <summary>The operation stopped at a safe boundary.</summary>
    Cancelled,

    /// <summary>The operation failed.</summary>
    Failed
}
