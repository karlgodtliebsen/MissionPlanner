namespace MissionPlanner.Core.Firmware;

/// <summary>Identifies the guarded firmware update workflow state.</summary>
public enum FirmwareUpdateState
{
    /// <summary>No update operation is active.</summary>
    Idle,
    /// <summary>The release manifest is being queried.</summary>
    Discovering,
    /// <summary>A package is downloading.</summary>
    Downloading,
    /// <summary>A downloaded package is being verified.</summary>
    Verifying,
    /// <summary>A verified package is ready for an adapter.</summary>
    ReadyToFlash,
    /// <summary>Normal MAVLink is being disconnected before flashing.</summary>
    WaitingForDisconnect,
    /// <summary>The platform adapter is flashing the board.</summary>
    Flashing,
    /// <summary>Flashing completed and the workflow is waiting for MAVLink reconnection.</summary>
    WaitingForReconnect,
    /// <summary>Flashing and post-flash reconnection completed.</summary>
    Completed,
    /// <summary>The operation was cancelled.</summary>
    Cancelled,
    /// <summary>The operation failed or was blocked.</summary>
    Failed
}
