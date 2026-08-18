namespace MissionPlanner.Core.Setup.MandatoryHardware;

/// <summary>Represents the per-compass calibration status projected from MAG_CAL_STATUS.</summary>
public enum CompassCalibrationStatus
{
    /// <summary>The compass has not begun calibration.</summary>
    NotStarted,

    /// <summary>The compass is queued and waiting to start.</summary>
    WaitingToStart,

    /// <summary>The compass is actively sampling.</summary>
    Running,

    /// <summary>The compass reported success.</summary>
    Success,

    /// <summary>The compass reported a generic failure.</summary>
    Failed,

    /// <summary>The compass reported a bad-orientation failure.</summary>
    BadOrientation,

    /// <summary>The compass reported a bad-radius failure.</summary>
    BadRadius
}
