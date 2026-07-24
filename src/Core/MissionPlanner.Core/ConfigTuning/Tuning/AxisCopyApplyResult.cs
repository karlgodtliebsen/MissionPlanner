namespace MissionPlanner.Core.ConfigTuning.Tuning;

/// <summary>Reports applying a previously reviewed axis-copy preview to pending state.</summary>
/// <param name="Success">Whether every preview value was accepted.</param>
/// <param name="Errors">Stale-preview, metadata, or coupled validation errors.</param>
public sealed record AxisCopyApplyResult(bool Success, IReadOnlyList<string> Errors);
