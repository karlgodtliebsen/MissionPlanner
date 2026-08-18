namespace MissionPlanner.Core.Setup.MandatoryHardware;

/// <summary>Identifies a supported accelerometer calibration workflow.</summary>
public enum AccelerometerCalibrationKind
{
    /// <summary>Six-position accelerometer calibration.</summary>
    SixPosition,

    /// <summary>Level-board trim calibration.</summary>
    Level
}
