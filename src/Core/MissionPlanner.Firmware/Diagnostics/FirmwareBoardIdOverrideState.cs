namespace MissionPlanner.Firmware.Diagnostics;

/// <summary>Describes whether the expert board-ID override was requested and required.</summary>
public enum FirmwareBoardIdOverrideState
{
    /// <summary>No override was requested.</summary>
    NotRequested,

    /// <summary>The override was requested but the detected and package board IDs matched.</summary>
    RequestedNotUsed,

    /// <summary>The override was requested and allowed an actual board-ID mismatch.</summary>
    Used
}
