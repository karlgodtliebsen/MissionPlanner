namespace MissionPlanner.Core.ConfigTuning.Tuning;

/// <summary>Describes one proposed target change in an axis-copy preview.</summary>
/// <param name="SourceParameter">The source parameter.</param>
/// <param name="TargetParameter">The target parameter.</param>
/// <param name="Component">The copied component.</param>
/// <param name="SourceValue">The proposed source value.</param>
/// <param name="TargetValue">The target pending value captured by the preview.</param>
public sealed record AxisCopyChange(
    string SourceParameter,
    string TargetParameter,
    string Component,
    double SourceValue,
    double TargetValue);
