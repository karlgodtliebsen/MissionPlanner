namespace MissionPlanner.Core.ConfigTuning.Comparison;

/// <summary>One source-labelled comparison result.</summary>
public sealed record ParameterComparisonRow(
    string Name,
    string DisplayName,
    string LeftSource,
    double? LeftValue,
    string RightSource,
    double? RightValue,
    double? Difference,
    ParameterComparisonStatus Status,
    string? Units,
    ParameterFieldMetadata? Metadata,
    bool CanStage,
    string? Message);
