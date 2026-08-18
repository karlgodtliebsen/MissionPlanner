namespace MissionPlanner.Core.Setup.MandatoryHardware;

/// <summary>Identifies the active and terminal stages of onboard compass calibration.</summary>
public enum CompassCalibrationWorkflowState
{
    /// <summary>No calibration has started.</summary>
    NotStarted,

    /// <summary>The start command is awaiting the first protocol evidence.</summary>
    Preparing,

    /// <summary>The vehicle is sampling one or more compasses.</summary>
    Running,

    /// <summary>Calibration succeeded but the results require explicit acceptance before they persist.</summary>
    PendingAcceptance,

    /// <summary>All calibrated compasses were accepted or auto-saved.</summary>
    Success,

    /// <summary>At least one compass explicitly failed calibration.</summary>
    Failed,

    /// <summary>The user cancelled calibration.</summary>
    Cancelled,

    /// <summary>The vehicle disconnected during calibration.</summary>
    Disconnected
}
