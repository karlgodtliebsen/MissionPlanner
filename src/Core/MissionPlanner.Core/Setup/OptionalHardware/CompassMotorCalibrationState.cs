namespace MissionPlanner.Core.Setup.OptionalHardware;

/// <summary>The state of a CompassMot calibration.</summary>
public enum CompassMotorCalibrationState
{
    /// <summary>No calibration is running.</summary>
    Idle,
    /// <summary>The start command was sent and is awaiting acknowledgement.</summary>
    Starting,
    /// <summary>The vehicle is collecting measurements.</summary>
    Running,
    /// <summary>The stop command was sent and is awaiting acknowledgement.</summary>
    Stopping,
    /// <summary>The calibration finished successfully.</summary>
    Completed,
    /// <summary>The calibration failed.</summary>
    Failed,
    /// <summary>The vehicle disconnected during the calibration.</summary>
    Disconnected
}