namespace MissionPlanner.Core.ConfigTuning;

/// <summary>A local parameter proposal with the exact live and pending values used for its review.</summary>
public sealed record ReviewedParameterEdit(string Name, double ExpectedLive, double ExpectedPending, double Value);
