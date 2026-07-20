using MissionPlanner.Core.Missions.Models;

namespace MissionPlanner.Core.Missions.Validation;

/// <summary>
/// Provides the public API for MissionValidationResult.
/// </summary>
public sealed record MissionValidationResult(IReadOnlyList<MissionValidationIssue> Issues)
{
    /// <summary>
    /// Provides the public API for IsValid.
    /// </summary>
    public bool IsValid => Issues.All(x => x.Severity != MissionValidationSeverity.Error);
}
