using MissionPlanner.Core.Missions.Models;

namespace MissionPlanner.Core.Missions.Validation;

/// <summary>
/// Provides the public API for MissionValidationIssue.
/// </summary>
public sealed record MissionValidationIssue(MissionValidationSeverity Severity, MissionItemId? ItemId, string Code, string Message);
