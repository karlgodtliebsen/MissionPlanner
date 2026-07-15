using MissionPlanner.Core.Missions.Models;

namespace MissionPlanner.Core.Missions.Validation;

public sealed record MissionValidationResult(IReadOnlyList<MissionValidationIssue> Issues)
{
    public bool IsValid => Issues.All(x => x.Severity != MissionValidationSeverity.Error);
}
