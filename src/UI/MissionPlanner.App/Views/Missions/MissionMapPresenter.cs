using System.ComponentModel;
using System.Diagnostics;
using Mapsui;
using Mapsui.Extensions;
using Mapsui.Projections;
using Mapsui.UI.Maui;
using MissionPlanner.App.Maps;
using MissionPlanner.Core.ConfigTuning.Planner;
using MissionPlanner.Core.Missions.Models;
using MissionPlanner.Core.Missions.Planning;
using MissionPlanner.Maps.Attribution;
using MissionPlanner.Maps.Sources;
using MissionPlanner.Maps.Terrain;

namespace MissionPlanner.App.Views.Missions;

/// <summary>
/// Owns Mapsui rendering and navigation for a mission-map view.
/// </summary>
internal sealed class MissionMapPresenter : IDisposable
{
    private const double WebMercatorInitialResolution = 156543.03392804097;
    private static readonly long PointerUpdateInterval = Stopwatch.Frequency / 30;
    private readonly MapView mapView;
    private readonly MissionMapViewModel viewModel;
    private readonly IPlannerSettingsService plannerSettings;
    private readonly Mapsui.Map map = new();
    private readonly MapBasemapController basemapController;
    private readonly IMapAttributionCoordinator attributionCoordinator;
    private readonly ITerrainElevationService terrainElevationService;
    private readonly Pin vehiclePin;
    private readonly List<Pin> missionPins = [];
    private Polyline routeLine;
    private readonly IReadOnlyDictionary<PlanningLayerKind, Polyline> planningLayers;
    private long lastPointerUpdate;
    private CancellationTokenSource? pointerElevationCancellation;
    private long pointerGeneration;
    private CancellationTokenSource? lifecycleCancellation;
    private bool active;
    private bool disposed;

    /// <summary>Initializes a presenter for a map view and shared mission editor.</summary>
    public MissionMapPresenter(MapView mapView, MissionMapViewModel viewModel, IPlannerSettingsService plannerSettings, IMapSourceResolver sourceResolver, IMapsuiBasemapFactory basemapFactory, IMapAttributionCoordinator attributionCoordinator, ITerrainElevationService terrainElevationService)
    {
        this.mapView = mapView;
        this.viewModel = viewModel;
        this.plannerSettings = plannerSettings;
        this.attributionCoordinator = attributionCoordinator;
        this.terrainElevationService = terrainElevationService;
        basemapController = new MapBasemapController(map, sourceResolver, basemapFactory, new MauiMapUiDispatcher(mapView.Dispatcher));

        attributionCoordinator.Changed += OnAttributionChanged;

        mapView.Map = map;

        vehiclePin = new Pin(mapView) { Label = "Vehicle", Type = PinType.Pin, Position = new Position(viewModel.VehicleLatitude, viewModel.VehicleLongitude) };
        mapView.Pins.Add(vehiclePin);
        routeLine = new Polyline { StrokeColor = Colors.OrangeRed, StrokeWidth = 3 };
        mapView.Drawables.Add(routeLine);
        planningLayers = CreatePlanningLayers();
        foreach (var layer in planningLayers.Values)
        {
            mapView.Drawables.Add(layer);
        }

        viewModel.PropertyChanged += OnViewModelPropertyChanged;
        viewModel.FitToMissionRequested += OnFitToMissionRequested;
        Render(viewModel.MapSnapshot);
        RenderPlanningOverlays(viewModel.PlanningOverlaySnapshot);
        //ActivateAsync().FireAndForget();
    }

    /// <summary>Starts asynchronous map-source work while the view is visible.</summary>
    public async Task ActivateAsync()
    {
        if (disposed || active)
        {
            return;
        }

        active = true;
        lifecycleCancellation = new CancellationTokenSource();
        await SwitchSourceAsync(viewModel.SelectedSourceId, lifecycleCancellation.Token);
    }

    /// <summary>Cancels map-source work when the view leaves the visual tree.</summary>
    private void Deactivate()
    {
        active = false;
        lifecycleCancellation?.Cancel();
        lifecycleCancellation?.Dispose();
        lifecycleCancellation = null;
        var elevationCancellation = pointerElevationCancellation;
        pointerElevationCancellation = null;
        elevationCancellation?.Cancel();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        Deactivate();
        disposed = true;
        viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        viewModel.FitToMissionRequested -= OnFitToMissionRequested;
        attributionCoordinator.Changed -= OnAttributionChanged;
        foreach (var pin in missionPins)
        {
            mapView.Pins.Remove(pin);
        }

        missionPins.Clear();
        mapView.Drawables.Remove(routeLine);
        foreach (var layer in planningLayers.Values)
        {
            mapView.Drawables.Remove(layer);
        }

        basemapController.Dispose();
    }


    /// <summary>Forwards a geographic primary click to the mission editor.</summary>
    public void HandleMapClick(double latitude, double longitude)
    {
        viewModel.HandleMapClick(latitude, longitude);
    }

    /// <summary>Updates the bindable pointer position from a Mapsui screen coordinate.</summary>
    public void UpdatePointerPosition(double x, double y)
    {
        var now = Stopwatch.GetTimestamp();
        if (now - lastPointerUpdate < PointerUpdateInterval)
        {
            return;
        }

        var viewport = map.Navigator.Viewport;
        if (viewport.Width <= 0 || viewport.Height <= 0)
        {
            return;
        }

        lastPointerUpdate = now;
        var world = viewport.ScreenToWorld(x, y);
        var (longitude, latitude) = SphericalMercator.ToLonLat(world.X, world.Y);
        viewModel.SetPointerPosition(latitude, longitude, null);
        viewModel.HandleMapPointerMove(latitude, longitude);
        viewModel.SetPointerElevationStatus(TerrainElevationStatus.Loading);
        RequestPointerElevation(latitude, longitude);
    }

    /// <summary>Centers the map on a geographic position.</summary>
    public void CenterOn(double latitude, double longitude, bool useDefaultZoom = false)
    {
        var (x, y) = SphericalMercator.FromLonLat(longitude, latitude);
        if (useDefaultZoom)
        {
            map.Navigator.CenterOnAndZoomTo(new MPoint(x, y), DefaultZoomResolution);
        }
        else
        {
            map.Navigator.CenterOn(new MPoint(x, y));
        }
    }

    /// <summary>Zooms the map in by one navigator step.</summary>
    public void ZoomIn()
    {
        map.Navigator.ZoomIn();
    }

    /// <summary>Zooms the map out by one navigator step.</summary>
    public void ZoomOut()
    {
        map.Navigator.ZoomOut();
    }

    /// <summary>Centers and zooms the map on the current vehicle position.</summary>
    public void ZoomToVehicle()
    {
        CenterOn(viewModel.VehicleLatitude, viewModel.VehicleLongitude, true);
    }

    /// <summary>Toggles automatic map following of the current vehicle.</summary>
    public void ToggleFollowVehicle()
    {
        viewModel.FollowVehicle = !viewModel.FollowVehicle;
    }


    private double DefaultZoomResolution => WebMercatorInitialResolution / Math.Pow(2, plannerSettings.Current.Map.DefaultZoom);

    private void RequestPointerElevation(double latitude, double longitude)
    {
        var generation = ++pointerGeneration;
        Debug.WriteLine($"[Terrain] generation={generation} status=Loading latitude={latitude:F7} longitude={longitude:F7}");
        _ = UpdatePointerElevationAsync(latitude, longitude, generation);
    }

    private async Task UpdatePointerElevationAsync(double latitude, double longitude, long generation)
    {
        CancellationTokenSource? lookupCancellation = null;
        try
        {
            // Generation-based debounce avoids a first-chance TaskCanceledException for every pointer event.
            await Task.Delay(250);
            if (generation != pointerGeneration || disposed)
            {
                Debug.WriteLine($"[Terrain] generation={generation} status=SupersededBeforeLookup");
                return;
            }

            pointerElevationCancellation?.Cancel();
            lookupCancellation = new CancellationTokenSource();
            pointerElevationCancellation = lookupCancellation;
            var result = await terrainElevationService.GetElevationAsync(latitude, longitude, lookupCancellation.Token);
            if (generation != pointerGeneration || disposed)
            {
                Debug.WriteLine($"[Terrain] generation={generation} tile={result.TileId ?? "none"} status=Superseded");
                return;
            }

            Debug.WriteLine($"[Terrain] generation={generation} tile={result.TileId ?? "none"} status={result.Status} elevation={result.ElevationMeters?.ToString("F1") ?? "null"}");
            await new MauiMapUiDispatcher(mapView.Dispatcher).InvokeAsync(
                () => viewModel.SetPointerElevation(result), lookupCancellation.Token);
        }
        catch (OperationCanceledException)
        {
            Debug.WriteLine($"[Terrain] generation={generation} status=Cancelled");
            // Pointer movement, view deactivation, or the bounded HTTP client cancelled the lookup.
        }
        finally
        {
            if (ReferenceEquals(pointerElevationCancellation, lookupCancellation))
            {
                pointerElevationCancellation = null;
            }

            lookupCancellation?.Dispose();
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName is nameof(MissionMapViewModel.VehicleLatitude) or nameof(MissionMapViewModel.VehicleLongitude))
        {
            var position = new Position(viewModel.VehicleLatitude, viewModel.VehicleLongitude);
            vehiclePin.Position = position;
            if (viewModel.FollowVehicle)
            {
                CenterOn(position.Latitude, position.Longitude);
            }
        }
        else if (args.PropertyName == nameof(MissionMapViewModel.SelectedSourceId))
        {
            _ = SwitchSourceAsync(viewModel.SelectedSourceId, lifecycleCancellation?.Token ?? CancellationToken.None);
        }
        else if (args.PropertyName == nameof(MissionMapViewModel.MapSnapshot))
        {
            Render(viewModel.MapSnapshot);
        }
        else if (args.PropertyName == nameof(MissionMapViewModel.PlanningOverlaySnapshot))
        {
            RenderPlanningOverlays(viewModel.PlanningOverlaySnapshot);
        }
    }

    private void OnFitToMissionRequested(object? sender, EventArgs args)
    {
        FitToMission();
    }

    private void Render(MissionMapSnapshot snapshot)
    {
        foreach (var pin in missionPins)
        {
            mapView.Pins.Remove(pin);
        }

        missionPins.Clear();
        foreach (var marker in snapshot.Markers)
        {
            var pin = new Pin(mapView)
            {
                Label = marker.Label,
                Type = PinType.Pin,
                Color = marker.Kind == MissionMapMarkerKind.Home ? Colors.Green : Colors.DodgerBlue,
                Scale = 0.7f,
                Position = new Position(marker.Position.LatitudeDegrees, marker.Position.LongitudeDegrees)
            };
            missionPins.Add(pin);
            mapView.Pins.Add(pin);
        }

        var replacement = new Polyline { StrokeColor = Colors.OrangeRed, StrokeWidth = 3 };
        foreach (var position in snapshot.Route)
        {
            replacement.Positions.Add(new Position(position.LatitudeDegrees, position.LongitudeDegrees));
        }

        mapView.Drawables.Remove(routeLine);
        mapView.Drawables.Add(replacement);
        routeLine = replacement;
        mapView.RefreshGraphics();
    }

    /// <summary>Applies a session-only viewport rotation in degrees.</summary>
    public void RotateTo(double degrees)
    {
        map.Navigator.RotateTo(degrees);
    }

    private IReadOnlyDictionary<PlanningLayerKind, Polyline> CreatePlanningLayers()
    {
        return new Dictionary<PlanningLayerKind, Polyline>
        {
            [PlanningLayerKind.Polygon] = NewPlanningLine(Colors.DeepSkyBlue, 3),
            [PlanningLayerKind.Measurement] = NewPlanningLine(Colors.Gold, 2),
            [PlanningLayerKind.Fence] = NewPlanningLine(Colors.Red, 3),
            [PlanningLayerKind.Rally] = NewPlanningLine(Colors.Orange, 3),
            [PlanningLayerKind.Poi] = NewPlanningLine(Colors.MediumPurple, 3),
            [PlanningLayerKind.Imported] = NewPlanningLine(Colors.Cyan, 2),
            [PlanningLayerKind.Survey] = NewPlanningLine(Colors.LimeGreen, 2),
            [PlanningLayerKind.TrackerHome] = NewPlanningLine(Colors.White, 3)
        };
    }

    private static Polyline NewPlanningLine(Color color, float width)
    {
        return new Polyline { StrokeColor = color, StrokeWidth = width };
    }

    private void RenderPlanningOverlays(MissionPlanningOverlaySnapshot snapshot)
    {
        SetPositions(planningLayers[PlanningLayerKind.Polygon], snapshot.DrawnPolygon, snapshot.DrawnPolygon.Count >= 3);
        SetPositions(planningLayers[PlanningLayerKind.Measurement], snapshot.TemporaryMeasurement);
        SetPositions(planningLayers[PlanningLayerKind.Fence], snapshot.FencePreview, snapshot.FencePreview.Count >= 3);
        SetPositions(planningLayers[PlanningLayerKind.Rally], snapshot.RallyPoints);
        SetPositions(planningLayers[PlanningLayerKind.Poi], snapshot.PoiItems);
        SetPositions(planningLayers[PlanningLayerKind.Imported], snapshot.ImportedOverlays.SelectMany(x => x.Positions).ToArray());
        SetPositions(planningLayers[PlanningLayerKind.Survey], snapshot.SurveyPreview);
        SetPositions(planningLayers[PlanningLayerKind.TrackerHome], snapshot.TrackerHome is { } home ? [home] : []);
        mapView.RefreshGraphics();
    }

    private static void SetPositions(Polyline line, IReadOnlyList<GeoPosition> positions, bool close = false)
    {
        line.Positions.Clear();
        foreach (var position in positions)
        {
            line.Positions.Add(new Position(position.LatitudeDegrees, position.LongitudeDegrees));
        }

        if (close && positions.Count > 0)
        {
            line.Positions.Add(new Position(positions[0].LatitudeDegrees, positions[0].LongitudeDegrees));
        }
    }

    internal enum PlanningLayerKind
    {
        Polygon,
        Measurement,
        Fence,
        Rally,
        Poi,
        Imported,
        Survey,
        TrackerHome
    }

    /// <summary>Fits the viewport to the current mission snapshot.</summary>
    public void FitToMission()
    {
        var snapshot = viewModel.MapSnapshot;
        if (snapshot.Route.Count == 1)
        {
            var position = snapshot.Route[0];
            CenterOn(position.LatitudeDegrees, position.LongitudeDegrees, true);
            return;
        }

        if (snapshot.Bounds is not { } bounds)
        {
            return;
        }

        var (minX, minY) = SphericalMercator.FromLonLat(bounds.West, bounds.South);
        var (maxX, maxY) = SphericalMercator.FromLonLat(bounds.East, bounds.North);
        map.Navigator.ZoomToBox(new MRect(minX, minY, maxX, maxY));
    }

    private async Task SwitchSourceAsync(string sourceId, CancellationToken cancellationToken)
    {
        if (!active || disposed)
        {
            return;
        }

        try
        {
            var result = await basemapController.SwitchAsync(sourceId, cancellationToken);
            if (result.IsSuccess && active && !disposed)
            {
                await attributionCoordinator.SetBasemapAsync(basemapController.CurrentResolvedSource, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // A newer selection or view deactivation owns the next map state.
        }
    }

    /// <summary>Toggles compact and expanded map attribution.</summary>
    public void ToggleAttribution()
    {
        attributionCoordinator.ToggleExpanded();
    }

    private void OnAttributionChanged(object? sender, MapAttributionOverlayState state)
    {
        viewModel.SetAttribution(state.DisplayText);
    }
}
