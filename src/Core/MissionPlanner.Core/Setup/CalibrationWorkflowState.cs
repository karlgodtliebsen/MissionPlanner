namespace MissionPlanner.Core.Setup;

/// <summary>Identifies the terminal and active stages of a calibration workflow.</summary>
public enum CalibrationWorkflowState
{
    /// <summary>No calibration has started.</summary>
    NotStarted,

    /// <summary>The start command is awaiting protocol acknowledgement.</summary>
    Preparing,

    /// <summary>The vehicle requested a specific physical orientation.</summary>
    WaitingForOrientation,

    /// <summary>The vehicle is sampling the confirmed orientation.</summary>
    Sampling,

    /// <summary>The vehicle is completing calibration.</summary>
    Completing,

    /// <summary>The protocol explicitly confirmed success.</summary>
    Success,

    /// <summary>The protocol rejected or explicitly failed calibration.</summary>
    Failed,

    /// <summary>The user cancelled calibration.</summary>
    Cancelled,

    /// <summary>The vehicle disconnected during calibration.</summary>
    Disconnected
}
