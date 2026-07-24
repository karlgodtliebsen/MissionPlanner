namespace MissionPlanner.Core.ConfigTuning.Osd;

/// <summary>Reports an OSD screen apply.</summary>
/// <param name="Success">Whether validation and all confirmed writes succeeded.</param>
/// <param name="ValidationIssues">The layout validation results.</param>
/// <param name="ParameterReport">The shared-session report, when writes were attempted.</param>
public sealed record OsdApplyResult(
    bool Success,
    IReadOnlyList<OsdValidationIssue> ValidationIssues,
    ParameterApplyReport? ParameterReport);
