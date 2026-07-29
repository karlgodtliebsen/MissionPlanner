namespace MissionPlanner.Core.ConfigTuning.Comparison;

/// <summary>A complete comparison retaining entries present on either side.</summary>
public sealed record ParameterComparisonResult(
    ParameterComparisonSource Left,
    ParameterComparisonSource Right,
    IReadOnlyList<ParameterComparisonRow> Rows,
    string? Warning);
