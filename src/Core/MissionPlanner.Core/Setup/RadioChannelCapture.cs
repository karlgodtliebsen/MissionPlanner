namespace MissionPlanner.Core.Setup;

/// <summary>Captures the observed endpoint extremes for one channel during calibration.</summary>
/// <param name="Number">The one-based channel number.</param>
/// <param name="Minimum">The lowest observed PWM.</param>
/// <param name="Maximum">The highest observed PWM.</param>
/// <param name="Current">The latest observed PWM.</param>
public sealed record RadioChannelCapture(int Number, int Minimum, int Maximum, int Current)
{
    /// <summary>Gets the captured travel range in microseconds.</summary>
    public int Range => Maximum - Minimum;
}
