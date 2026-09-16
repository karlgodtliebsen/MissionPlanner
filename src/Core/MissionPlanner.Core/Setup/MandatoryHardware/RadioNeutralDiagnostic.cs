namespace MissionPlanner.Core.Setup.MandatoryHardware;

/// <summary>Read-only neutral diagnostics for a mapped Roll, Pitch, or Yaw input.</summary>
/// <param name="Current">Latest input in microseconds.</param>
/// <param name="ObservedCenter">User-positioned center sampled during calibration review.</param>
/// <param name="ConfiguredTrim">Known configured trim; null before parameter download.</param>
/// <param name="DeadZone">Known configured dead-zone in microseconds.</param>
/// <param name="ObservedMinimum">Captured minimum, when available.</param>
/// <param name="ObservedMaximum">Captured maximum, when available.</param>
/// <param name="IsStale">Whether live input is too old for a neutral assessment.</param>
public sealed record RadioNeutralDiagnostic(
    int Current,
    int? ObservedCenter,
    int? ConfiguredTrim,
    int? DeadZone,
    int? ObservedMinimum,
    int? ObservedMaximum,
    bool IsStale)
{
    /// <summary>Gets observed center minus configured trim, in microseconds.</summary>
    public int? CenterError => ObservedCenter - ConfiguredTrim;

    /// <summary>Gets whether fresh live input lies within the configured dead-zone.</summary>
    public bool? NeutralAllowed => IsStale || ConfiguredTrim is null || DeadZone is null or < 0
        ? null
        : Math.Abs(Current - ConfiguredTrim.Value) <= DeadZone.Value;

    /// <summary>Gets whether the observed review center lies outside the configured dead-zone.</summary>
    public bool? CenterOutsideDeadZone => CenterError is not { } error || DeadZone is null or < 0
        ? null
        : Math.Abs(error) > DeadZone.Value;

    /// <summary>Gets upper travel minus lower travel around observed center, in microseconds.</summary>
    public int? Asymmetry => ObservedMaximum + ObservedMinimum - 2 * ObservedCenter;
}
