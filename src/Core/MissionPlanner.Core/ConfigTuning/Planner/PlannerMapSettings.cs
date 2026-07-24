namespace MissionPlanner.Core.ConfigTuning.Planner;

/// <summary>Configures map defaults.</summary>
public sealed record PlannerMapSettings
{
    /// <summary>Gets the preferred map provider.</summary>
    public PlannerMapProvider Provider { get; init; } = PlannerMapProvider.OpenStreetMap;

    /// <summary>Gets the preferred map style.</summary>
    public PlannerMapStyle Style { get; init; } = PlannerMapStyle.Standard;

    /// <summary>Gets the initial map zoom level.</summary>
    public double DefaultZoom { get; init; } = 16;
}
