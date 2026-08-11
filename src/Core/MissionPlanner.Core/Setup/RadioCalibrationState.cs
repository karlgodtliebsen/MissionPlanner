namespace MissionPlanner.Core.Setup;

/// <summary>Identifies the active and terminal stages of RC radio calibration.</summary>
public enum RadioCalibrationState
{
    /// <summary>No calibration has started.</summary>
    NotStarted,

    /// <summary>Live stick movement is being captured for endpoint extremes.</summary>
    Capturing,

    /// <summary>Captured endpoints are ready for validation and review.</summary>
    Review,

    /// <summary>Captured endpoints are being written and confirmed.</summary>
    Writing,

    /// <summary>Endpoints were written and confirmed by readback.</summary>
    Success,

    /// <summary>Validation or the confirmed write failed.</summary>
    Failed,

    /// <summary>The user cancelled calibration.</summary>
    Cancelled,

    /// <summary>The vehicle disconnected during calibration.</summary>
    Disconnected
}
