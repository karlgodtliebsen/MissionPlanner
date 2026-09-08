using MissionPlanner.Core.ConfigTuning.Planner;
using MissionPlanner.Maps.Http;

namespace MissionPlanner.App.Maps;

/// <summary>Projects live Planner settings into the map HTTP pipeline.</summary>
public sealed class PlannerMapHttpRuntimeSettings(IPlannerSettingsService plannerSettings) : IMapHttpRuntimeSettings
{
    /// <inheritdoc />
    public bool CacheEnabled => plannerSettings.Current.Map.HttpCacheEnabled;

    /// <inheritdoc />
    public long CacheLimitBytes => plannerSettings.Current.Map.HttpCacheLimitBytes;
}
