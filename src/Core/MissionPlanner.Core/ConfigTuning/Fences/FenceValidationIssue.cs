namespace MissionPlanner.Core.ConfigTuning.Fences;

/// <summary>Describes one fence validation problem.</summary>
/// <param name="Code">The stable problem code.</param>
/// <param name="Message">The user-facing explanation.</param>
/// <param name="AreaId">The affected area, when applicable.</param>
public sealed record FenceValidationIssue(string Code, string Message, Guid? AreaId = null);
