namespace MissionPlanner.Core.Missions.Rally;

/// <summary>An ordered rally plan, separate from the flight mission.</summary>
public sealed record RallyPlan(IReadOnlyList<RallyPoint> Points)
{ /// <summary>Empty plan.</summary>
    public static RallyPlan Empty { get; } = new([]);
}