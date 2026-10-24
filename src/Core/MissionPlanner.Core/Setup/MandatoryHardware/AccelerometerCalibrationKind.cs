namespace MissionPlanner.Core.Setup.MandatoryHardware;

/// <summary>Identifies a supported accelerometer calibration workflow.</summary>
public enum AccelerometerCalibrationKind
{
    /// <summary>Six-position accelerometer calibration.</summary>
    SixPosition,

    /// <summary>Level-board trim calibration.</summary>
    Level,

    /// <summary>Stationary simple accelerometer calibration requested with PREFLIGHT_CALIBRATION parameter 5 equal to 4.</summary>
    Simple
}
