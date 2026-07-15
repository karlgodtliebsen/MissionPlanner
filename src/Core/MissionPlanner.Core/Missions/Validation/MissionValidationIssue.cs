using MissionPlanner.Core.Missions.Models;

namespace MissionPlanner.Core.Missions.Validation;

public sealed record MissionValidationIssue(MissionValidationSeverity Severity, MissionItemId? ItemId, string Code, string Message);
