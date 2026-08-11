namespace MissionPlanner.Maps.Prefetch;

/// <summary>Central safety limits for map cache warming.</summary>
public static class MapPrefetchLimits
{
    /// <summary>Maximum tiles accepted by one prefetch operation.</summary>
    public const int MaximumTiles = 10_000;
}
