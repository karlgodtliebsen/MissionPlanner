using MissionPlanner.Core.ConfigTuning.Fences;
using MissionPlanner.Core.Missions.Models;
using MissionPlanner.Core.Missions.Planning;
using MissionPlanner.Core.Missions.Rally;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Maps.Coordinates;
using MissionPlanner.Maps.Terrain;

namespace MissionPlanner.App.Views.Missions;

/// <summary>The result of offering a primary map click to the active planning interaction.</summary>
/// <param name="Consumed">Whether the interaction took the click, so it must not also add a waypoint.</param>
/// <param name="Message">The status message to show, when the click completed an interaction.</param>
public sealed record MapClickOutcome(bool Consumed, string? Message)
{
    /// <summary>Gets the outcome for a click that no interaction took.</summary>
    public static MapClickOutcome NotConsumed { get; } = new(false, null);
}

/// <summary>
/// Owns the map interaction state of one mission editor: the active planning interaction, the
/// planning polygon it draws, the pending rally altitude, and the overlay shown over the map.
/// </summary>
public interface IMissionMapInteractionCoordinator
{
    /// <summary>Raised when the interaction state or its temporary overlay changed.</summary>
    event EventHandler? Changed;

    /// <summary>Gets the planning polygon of this editor.</summary>
    IPlanningPolygonService Polygons { get; }

    /// <summary>Gets the current interaction state.</summary>
    MissionMapInteractionState State { get; }

    /// <summary>Gets or sets the altitude applied to the next rally point placed on the map.</summary>
    MissionAltitude PendingRallyAltitude { get; set; }

    /// <summary>Starts an interaction in the given mode.</summary>
    void Begin(MissionMapInteractionMode mode, string prompt);

    /// <summary>Cancels the current interaction and clears its temporary overlay.</summary>
    void Cancel();

    /// <summary>Completes the current interaction, keeping its results.</summary>
    void Complete();

    /// <summary>Routes pointer movement to the active interaction.</summary>
    void MovePointer(GeoPosition position);

    /// <summary>Offers a primary click to the active interaction and applies the result when it completes one.</summary>
    MapClickOutcome HandleClick(GeoPosition position);

    /// <summary>Composes the renderer-independent overlay from the interaction and the local planning state.</summary>
    /// <param name="importedOverlays">Imported overlay features to draw.</param>
    /// <param name="surveyPreview">The generated mission preview to draw.</param>
    MissionPlanningOverlaySnapshot ComposeOverlay(IReadOnlyList<ImportedPlanningOverlay> importedOverlays, IReadOnlyList<GeoPosition> surveyPreview);
}

/// <summary>Default <see cref="IMissionMapInteractionCoordinator"/>.</summary>
public sealed class MissionMapInteractionCoordinator : IMissionMapInteractionCoordinator
{
    private readonly IMissionMapInteractionService interactionService;
    private readonly IActiveVehicleContext activeVehicle;
    private readonly IFenceConfigurationService fenceService;
    private readonly IRallyConfigurationService rallyService;
    private readonly IPoiService poiService;
    private readonly ITrackerHomeService trackerHomeService;

    /// <summary>Initializes a new instance of the <see cref="MissionMapInteractionCoordinator"/> class.</summary>
    public MissionMapInteractionCoordinator(
        IMissionMapInteractionService interactionService,
        IPlanningPolygonService polygonService,
        IActiveVehicleContext activeVehicle,
        IFenceConfigurationService fenceService,
        IRallyConfigurationService rallyService,
        IPoiService poiService,
        ITrackerHomeService trackerHomeService)
    {
        this.interactionService = interactionService;
        Polygons = polygonService;
        this.activeVehicle = activeVehicle;
        this.fenceService = fenceService;
        this.rallyService = rallyService;
        this.poiService = poiService;
        this.trackerHomeService = trackerHomeService;
    }

    /// <inheritdoc />
    public event EventHandler? Changed
    {
        add => interactionService.Changed += value;
        remove => interactionService.Changed -= value;
    }

    /// <inheritdoc />
    public IPlanningPolygonService Polygons { get; }

    /// <inheritdoc />
    public MissionMapInteractionState State => interactionService.State;

    /// <inheritdoc />
    public MissionAltitude PendingRallyAltitude { get; set; }

    /// <inheritdoc />
    public void Begin(MissionMapInteractionMode mode, string prompt)
    {
        interactionService.Enter(mode, prompt);
    }

    /// <inheritdoc />
    public void Cancel()
    {
        interactionService.Cancel();
    }

    /// <inheritdoc />
    public void Complete()
    {
        interactionService.Complete();
    }

    /// <inheritdoc />
    public void MovePointer(GeoPosition position)
    {
        interactionService.MovePointer(position);
    }

    /// <inheritdoc />
    public MapClickOutcome HandleClick(GeoPosition position)
    {
        var mode = interactionService.State.Mode;
        if (!interactionService.AcceptClick(position))
        {
            return MapClickOutcome.NotConsumed;
        }

        if (mode == MissionMapInteractionMode.SetFenceReturnLocation && activeVehicle.VehicleId is { } vehicleId)
        {
            var snapshot = fenceService.GetSnapshot(vehicleId);
            fenceService.SetLocalPlan(vehicleId, snapshot.LocalPlan with
            {
                ReturnPoint = position
            });
            interactionService.Complete();
            return new MapClickOutcome(true, "Fence return location updated locally; upload to apply it.");
        }

        if (mode == MissionMapInteractionMode.SetRallyPoint && activeVehicle.VehicleId is { } rallyVehicleId)
        {
            var snapshot = rallyService.GetSnapshot(rallyVehicleId);
            var point = new RallyPoint(RallyPointId.New(), position, PendingRallyAltitude);
            rallyService.SetLocalPlan(rallyVehicleId, new RallyPlan(snapshot.LocalPlan.Points.Append(point).ToArray()));
            interactionService.Complete();
            return new MapClickOutcome(true, "Rally point added locally; upload to apply it.");
        }

        if (mode == MissionMapInteractionMode.MeasureDistance && interactionService.State.Positions.Count >= 2)
        {
            var (distance, bearing) = GeographicCalculations.CalculateDistanceAndBearing(
                interactionService.State.Positions[0], interactionService.State.Positions[1]);
            interactionService.Complete();
            return new MapClickOutcome(true, $"Distance {distance:F1} m • initial bearing {bearing:F1}°.");
        }

        return new MapClickOutcome(true, null);
    }

    /// <inheritdoc />
    public MissionPlanningOverlaySnapshot ComposeOverlay(IReadOnlyList<ImportedPlanningOverlay> importedOverlays, IReadOnlyList<GeoPosition> surveyPreview)
    {
        var overlay = interactionService.Overlay;
        var vertices = interactionService.State.Mode == MissionMapInteractionMode.DrawPolygon
            ? overlay.DrawnPolygon
            : Polygons.Snapshot.Polygon?.Vertices ?? [];
        var fence = activeVehicle.VehicleId is { } vehicleId ? FenceOutline(fenceService.GetSnapshot(vehicleId).LocalPlan) : [];
        var rally = activeVehicle.VehicleId is { } rallyVehicleId
            ? rallyService.GetSnapshot(rallyVehicleId).LocalPlan.Points.Select(point => point.Position).ToArray()
            : [];
        var pois = poiService.Snapshot.Items.Select(item => item.Position).ToArray();
        return overlay with
        {
            DrawnPolygon = vertices,
            FencePreview = fence,
            RallyPoints = rally,
            PoiItems = pois,
            ImportedOverlays = importedOverlays,
            SurveyPreview = surveyPreview,
            TrackerHome = trackerHomeService.Snapshot?.Position
        };
    }

    /// <summary>Builds the outline drawn for the first local fence area, or the return point when there is none.</summary>
    public static IReadOnlyList<GeoPosition> FenceOutline(FencePlan plan)
    {
        var area = plan.Areas.FirstOrDefault();
        if (area is null)
        {
            return plan.ReturnPoint is { } point ? [point] : [];
        }

        if (area.Vertices.Count > 0)
        {
            return area.Vertices;
        }

        if (area.Center is not { } center || area.RadiusMeters <= 0)
        {
            return [];
        }

        const double earthRadius = 6378137d;
        return Enumerable.Range(0, 36).Select(index =>
        {
            var angle = index * Math.PI * 2d / 36d;
            var latitude = center.LatitudeDegrees + (area.RadiusMeters * Math.Cos(angle) / earthRadius * 180d / Math.PI);
            var longitude = center.LongitudeDegrees + (area.RadiusMeters * Math.Sin(angle) / (earthRadius * Math.Cos(center.LatitudeDegrees * Math.PI / 180d)) * 180d / Math.PI);
            return new GeoPosition(latitude, longitude);
        }).ToArray();
    }
}

/// <summary>Formats the map pointer coordinate and terrain lookup state for the status bar.</summary>
public static class MapPointerPresentation
{
    /// <summary>The text shown while there is no pointer position.</summary>
    public const string PositionUnavailable = "Position unavailable";

    /// <summary>Formats the pointer position in the given coordinate style.</summary>
    public static string CoordinateText(string style, double? latitude, double? longitude)
    {
        return latitude is { } lat && longitude is { } lon
            ? MapCoordinateFormatter.Format(style, lat, lon)
            : PositionUnavailable;
    }

    /// <summary>Formats the typed terrain lookup status, appending the service message when the lookup did not succeed.</summary>
    public static string TerrainStatusText(TerrainElevationStatus status, string? message)
    {
        var text = status switch
        {
            TerrainElevationStatus.Loading => "Terrain: loading",
            TerrainElevationStatus.Available => "Terrain: available",
            TerrainElevationStatus.OutsideCoverage => "Terrain: outside coverage",
            TerrainElevationStatus.NetworkUnavailable => "Terrain: network unavailable",
            TerrainElevationStatus.InvalidData => "Terrain: invalid data",
            var _ => string.Empty
        };
        if (!string.IsNullOrWhiteSpace(message) && status is not TerrainElevationStatus.Available)
        {
            text += $" ({message})";
        }

        return text;
    }
}
