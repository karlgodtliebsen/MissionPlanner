using MissionPlanner.Core.Missions.Models;

namespace MissionPlanner.Core.Missions.Planning;

/// <summary>Describes a named imported line or polygon without renderer-specific geometry.</summary>
/// <param name="Name">Feature display name.</param>
/// <param name="Positions">Ordered geographic positions.</param>
/// <param name="IsClosed">Whether the feature is a closed polygon.</param>
public sealed record ImportedPlanningOverlay(string Name, IReadOnlyList<GeoPosition> Positions, bool IsClosed);

/// <summary>Immutable, UI-neutral state for non-mission planning overlays.</summary>
/// <param name="DrawnPolygon">Current planning polygon or polygon drawing preview.</param>
/// <param name="TemporaryMeasurement">Current measurement vertices.</param>
/// <param name="FencePreview">Fence preview vertices.</param>
/// <param name="RallyPoints">Rally-point positions.</param>
/// <param name="PoiItems">Point-of-interest positions.</param>
/// <param name="ImportedOverlays">Imported KML/SHP visual features.</param>
/// <param name="SurveyPreview">Generated survey preview route.</param>
/// <param name="TrackerHome">Antenna-tracker home position.</param>
public sealed record MissionPlanningOverlaySnapshot(
    IReadOnlyList<GeoPosition> DrawnPolygon,
    IReadOnlyList<GeoPosition> TemporaryMeasurement,
    IReadOnlyList<GeoPosition> FencePreview,
    IReadOnlyList<GeoPosition> RallyPoints,
    IReadOnlyList<GeoPosition> PoiItems,
    IReadOnlyList<ImportedPlanningOverlay> ImportedOverlays,
    IReadOnlyList<GeoPosition> SurveyPreview,
    GeoPosition? TrackerHome)
{
    /// <summary>Gets an overlay snapshot containing no planning features.</summary>
    public static MissionPlanningOverlaySnapshot Empty { get; } = new([], [], [], [], [], [], [], null);
}
