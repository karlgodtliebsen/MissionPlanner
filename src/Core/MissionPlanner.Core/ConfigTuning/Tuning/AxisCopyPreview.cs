namespace MissionPlanner.Core.ConfigTuning.Tuning;

/// <summary>Represents a non-mutating, scope-bound axis-copy preview.</summary>
/// <param name="Scope">The vehicle and firmware scope.</param>
/// <param name="DescriptorKey">The descriptor.</param>
/// <param name="SourceAxis">The source axis.</param>
/// <param name="TargetAxis">The target axis.</param>
/// <param name="Changes">The proposed component changes.</param>
public sealed record AxisCopyPreview(
    ParameterEditScope Scope,
    string DescriptorKey,
    string SourceAxis,
    string TargetAxis,
    IReadOnlyList<AxisCopyChange> Changes);
