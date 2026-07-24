namespace MissionPlanner.Core.ConfigTuning.Tuning;

/// <summary>Reports a group-scoped apply operation.</summary>
/// <param name="Success">Whether validation and all writes succeeded.</param>
/// <param name="ValidationIssues">Coupled validation failures.</param>
/// <param name="ParameterReport">The shared-session write report, when writes were attempted.</param>
public sealed record BasicTuningApplyResult(
    bool Success,
    IReadOnlyList<BasicTuningValidationIssue> ValidationIssues,
    ParameterApplyReport? ParameterReport);
