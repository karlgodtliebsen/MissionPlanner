namespace MissionPlanner.Core.Setup.MandatoryHardware;

/// <summary>Captures the observed endpoint extremes for one channel during calibration.</summary>
/// <param name="Number">The one-based channel number.</param>
/// <param name="Minimum">The lowest observed PWM.</param>
/// <param name="Maximum">The highest observed PWM.</param>
/// <param name="Current">The latest observed PWM.</param>
public sealed record RadioChannelCapture(int Number, int Minimum, int Maximum, int Current)
{
    /// <summary>Gets the captured travel range in microseconds.</summary>
    public int Range => Maximum - Minimum;

    /// <summary>Gets the fresh trim candidate sampled during Review, when available.</summary>
    public int? CandidateTrim { get; init; }

    /// <summary>Gets the resolved pilot function, when assigned.</summary>
    public string? FunctionName { get; init; }

    /// <summary>Gets the trim policy derived from vehicle and channel semantics.</summary>
    public RadioTrimPolicy TrimPolicy { get; init; } = RadioTrimPolicy.Current;

    /// <summary>Gets validation issues associated specifically with this channel.</summary>
    public IReadOnlyList<RadioValidationIssue> Issues { get; init; } = [];
}
