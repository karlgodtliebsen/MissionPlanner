namespace MissionPlanner.Core.ConfigTuning.Planner;

/// <summary>Configures display units.</summary>
public sealed record PlannerUnitSettings
{
    /// <summary>Gets the preferred unit system.</summary>
    public UnitSystem System { get; init; } = UnitSystem.Metric;
}
