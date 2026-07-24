namespace MissionPlanner.Core.ConfigTuning.Planner;

/// <summary>Configures parameter-cache behavior.</summary>
public sealed record PlannerParameterCacheSettings
{
    /// <summary>Gets the cache policy.</summary>
    public ParameterCachePolicy Policy { get; init; } = ParameterCachePolicy.PreferRecentCache;

    /// <summary>Gets the maximum accepted cache age in minutes.</summary>
    public int MaximumAgeMinutes { get; init; } = 30;
}
