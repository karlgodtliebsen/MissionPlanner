namespace MissionPlanner.Core.Setup;

/// <summary>Identifies one physical orientation in six-position accelerometer calibration.</summary>
public enum CalibrationOrientation
{
    /// <summary>Vehicle level on its landing gear.</summary>
    Level = 1,

    /// <summary>Vehicle resting on its left side.</summary>
    Left = 2,

    /// <summary>Vehicle resting on its right side.</summary>
    Right = 3,

    /// <summary>Vehicle nose pointing down.</summary>
    NoseDown = 4,

    /// <summary>Vehicle nose pointing up.</summary>
    NoseUp = 5,

    /// <summary>Vehicle resting upside down on its back.</summary>
    Back = 6
}
