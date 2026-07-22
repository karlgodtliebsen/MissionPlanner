namespace MissionPlanner.Core.Setup;

/// <summary>Configures bounded waits in the onboard compass calibration protocol.</summary>
public sealed class CompassCalibrationOptions
{
    /// <summary>Gets the configuration section name.</summary>
    public const string SectionName = "CompassCalibration";

    /// <summary>Gets or sets the maximum wait for the vehicle to accept calibration startup.</summary>
    public TimeSpan StartTimeout { get; set; } = TimeSpan.FromSeconds(8);
}
