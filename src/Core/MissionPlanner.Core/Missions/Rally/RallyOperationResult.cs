namespace MissionPlanner.Core.Missions.Rally;

/// <summary>Result of a rally transfer operation.</summary>
public sealed record RallyOperationResult(bool Success, string Message, RallyPlanSnapshot Snapshot);