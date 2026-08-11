namespace MissionPlanner.Core.ConfigTuning.Planner;

/// <summary>Configures map defaults.</summary>
public sealed record PlannerMapSettings
{
    /// <summary>Gets the stable identifier of the selected catalog, custom, or offline source.</summary>
    public string SelectedSourceId { get; init; } = "osm-standard";

    /// <summary>Gets whether the bounded HTTP tile cache is enabled.</summary>
    public bool HttpCacheEnabled { get; init; } = true;

    /// <summary>Gets the HTTP tile-cache disk limit in bytes.</summary>
    public long HttpCacheLimitBytes { get; init; } = 268_435_456;

    /// <summary>Gets the preferred map provider.</summary>
    public PlannerMapProvider Provider { get; init; } = PlannerMapProvider.OpenStreetMap;

    /// <summary>Gets the preferred map style.</summary>
    public PlannerMapStyle Style { get; init; } = PlannerMapStyle.Standard;

    /// <summary>Gets the initial map zoom level.</summary>
    public double DefaultZoom { get; init; } = 16;
}
