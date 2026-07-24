namespace MissionPlanner.Core.ConfigTuning.Fences;

/// <summary>Reports a fence download, upload, or clear operation.</summary>
/// <param name="Success">Whether the operation completed and was confirmed.</param>
/// <param name="Message">The user-facing result.</param>
/// <param name="Snapshot">The resulting workspace snapshot.</param>
/// <param name="Validation">The validation result.</param>
/// <param name="ParameterReport">The grouped parameter result, when parameters were applied.</param>
public sealed record FenceOperationReport(
    bool Success,
    string Message,
    FenceConfigurationSnapshot Snapshot,
    FenceValidationResult Validation,
    ParameterApplyReport? ParameterReport = null);
