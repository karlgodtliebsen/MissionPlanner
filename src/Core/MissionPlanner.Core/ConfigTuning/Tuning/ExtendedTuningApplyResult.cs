namespace MissionPlanner.Core.ConfigTuning.Tuning;

/// <summary>Reports an advanced group apply.</summary>
/// <param name="Success">Whether validation and confirmed writes succeeded.</param>
/// <param name="ValidationIssues">The validation failures.</param>
/// <param name="ParameterReport">The shared-session write result, when writes were attempted.</param>
public sealed record ExtendedTuningApplyResult(
    bool Success,
    IReadOnlyList<ExtendedTuningValidationIssue> ValidationIssues,
    ParameterApplyReport? ParameterReport);
