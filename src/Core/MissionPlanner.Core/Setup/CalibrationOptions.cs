namespace MissionPlanner.Core.Setup;

/// <summary>Configures bounded waits in interactive calibration protocols.</summary>
public sealed class CalibrationOptions
{
    /// <summary>Gets the configuration section name.</summary>
    public const string SectionName = "Calibration";

    /// <summary>Gets or sets the maximum wait for initial protocol evidence.</summary>
    public TimeSpan StartTimeout { get; set; } = TimeSpan.FromSeconds(8);

    /// <summary>Gets or sets the maximum wait for terminal level-calibration acknowledgement.</summary>
    public TimeSpan LevelTimeout { get; set; } = TimeSpan.FromSeconds(30);
}
