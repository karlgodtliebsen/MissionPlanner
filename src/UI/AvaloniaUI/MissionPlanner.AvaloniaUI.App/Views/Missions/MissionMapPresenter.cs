using System.ComponentModel;
using System.Diagnostics;
using Mapsui;
using Mapsui.Extensions;
using Mapsui.Layers;
using Mapsui.Nts;
using Mapsui.Projections;
using Mapsui.Styles;
using Mapsui.UI.Avalonia;
using NetTopologySuite.Geometries;
using MissionPlanner.AvaloniaUI.App.Maps;
using MissionPlanner.Core.ConfigTuning.Planner;
using MissionPlanner.Core.Missions.Models;
using MissionPlanner.Core.Missions.Planning;
using MissionPlanner.Maps.Attribution;
using MissionPlanner.Maps.Sources;
using MissionPlanner.Maps.Terrain;

namespace MissionPlanner.AvaloniaUI.App.Views.Missions;

/// <summary>
/// Owns Mapsui rendering and navigation for a mission-map view.
/// </summary>
internal sealed class MissionMapPresenter : IDisposable
{
    private const double WebMercatorInitialResolution = 156543.03392804097;
    private static readonly long pointerUpdateInterval = Stopwatch.Frequency / 30;
    private readonly MapControl mapView;
    private readonly MissionMapViewModel viewModel;
    private readonly IPlannerSettingsService plannerSettings;
    private readonly Mapsui.Map map = new();
    private readonly MapBasemapController basemapController;
    private readonly IMapAttributionCoordinator attributionCoordinator;
    private readonly ITerrainElevationService terrainElevationService;
    private readonly MemoryLayer vehicleLayer = NewLayer("Vehicle");
    private readonly MemoryLayer markerLayer = NewLayer("Mission markers");
    private readonly MemoryLayer routeLayer = NewLayer("Mission route");
    private readonly IReadOnlyDictionary<PlanningLayerKind, MemoryLayer> planningLayers;
    private long lastPointerUpdate;
    private CancellationTokenSource? pointerElevationCancellation;
    private long pointerGeneration;
    private CancellationTokenSource? lifecycleCancellation;
    private Action? pendingNavigation;
    private bool basemapRefreshPending;
    private bool isActive;
    private bool disposed;

    /// <summary>
    /// Initializes a presenter for a map view and shared mission editor.
    /// </summary>
    public MissionMapPresenter(MapControl mapView, MissionMapViewModel viewModel, IPlannerSettingsService plannerSettings, IMapSourceResolver sourceResolver,
        IMapsuiBasemapFactory basemapFactory, IMapAttributionCoordinator attributionCoordinator, ITerrainElevationService terrainElevationService)
    {
        this.mapView = mapView;
        this.viewModel = viewModel;
        this.plannerSettings = plannerSettings;
        this.attributionCoordinator = attributionCoordinator;
        this.terrainElevationService = terrainElevationService;
        mapView.Map = map;
        basemapController = new MapBasemapController(map, sourceResolver, basemapFactory, new AvaloniaMapUiDispatcher(mapView.Dispatcher));
        map.Layers.Add(vehicleLayer);
        map.Layers.Add(routeLayer);
        map.Layers.Add(markerLayer);
        planningLayers = CreatePlanningLayers();
        foreach (var layer in planningLayers.Values)
        {
            map.Layers.Add(layer);
        }
    }

    /// <summary>
    /// Starts asynchronous map-source work while the view is visible.
    /// </summary>
    public async Task ActivateAsync(CancellationToken cancellationToken = default)
    {
        if (disposed || isActive)
        {
            return;
        }

        isActive = true;
        lifecycleCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        pointerElevationCancellation = new CancellationTokenSource();

        attributionCoordinator.Changed += OnAttributionChanged;
        mapView.SizeChanged += OnMapViewSizeChanged;
        viewModel.PropertyChanged += OnViewModelPropertyChanged;
        viewModel.FitToMissionRequested += OnFitToMissionRequested;
        Render(viewModel.MapSnapshot);
        RenderPlanningOverlays(viewModel.PlanningOverlaySnapshot);

        await SwitchSourceAsync(viewModel.SelectedSourceId, lifecycleCancellation.Token);
    }

    /// <summary>
    /// Cancels map-source work when the view leaves the visual tree.
    /// </summary>
    public void Deactivate()
    {
        if (disposed || !isActive)
        {
            return;
        }
        isActive = false;
        lifecycleCancellation?.Cancel();
        lifecycleCancellation?.Dispose();
        lifecycleCancellation = null;
        var elevationCancellation = pointerElevationCancellation;
        pointerElevationCancellation = null;
        elevationCancellation?.Cancel();
        attributionCoordinator.Changed -= OnAttributionChanged;
        mapView.SizeChanged -= OnMapViewSizeChanged;
        viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        viewModel.FitToMissionRequested -= OnFitToMissionRequested;
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
        pendingNavigation = null;
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
        if (now - lastPointerUpdate < pointerUpdateInterval)
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

    /// <summary>
    /// Centers the map on a geographic position.
    /// </summary>
    public void CenterOn(double latitude, double longitude, bool useDefaultZoom = false)
    {
        var (x, y) = SphericalMercator.FromLonLat(longitude, latitude);
        Navigate(() =>
        {
            if (useDefaultZoom)
            {
                map.Navigator.CenterOnAndZoomTo(new MPoint(x, y), DefaultZoomResolution);
            }
            else
            {
                map.Navigator.CenterOn(new MPoint(x, y));
            }
        });
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
            pointerElevationCancellation?.Cancel();
            lookupCancellation = new CancellationTokenSource();

            // Generation-based debounce avoids a first-chance TaskCanceledException for every pointer event.
            await Task.Delay(250, lookupCancellation.Token);
            if (generation != pointerGeneration || disposed)
            {
                Debug.WriteLine($"[Terrain] generation={generation} status=SupersededBeforeLookup");
                return;
            }

            pointerElevationCancellation = lookupCancellation;
            var result = await terrainElevationService.GetElevationAsync(latitude, longitude, lookupCancellation.Token);
            if (generation != pointerGeneration || disposed)
            {
                Debug.WriteLine($"[Terrain] generation={generation} tile={result.TileId ?? "none"} status=Superseded");
                return;
            }

            Debug.WriteLine($"[Terrain] generation={generation} tile={result.TileId ?? "none"} status={result.Status} elevation={result.ElevationMeters?.ToString("F1") ?? "null"}");
            await new AvaloniaMapUiDispatcher(mapView.Dispatcher).InvokeAsync(
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
            SetPointFeatures(vehicleLayer, [(viewModel.VehicleLatitude, viewModel.VehicleLongitude, "Vehicle")], Mapsui.Styles.Color.Red);
            if (viewModel.FollowVehicle)
            {
                CenterOn(viewModel.VehicleLatitude, viewModel.VehicleLongitude);
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

    private void OnFitToMissionRequested()
    {
        Debug.WriteLine($"[MissionMap] Fit requested: route={viewModel.MapSnapshot.Route.Count}, bounds={viewModel.MapSnapshot.Bounds}, viewport={map.Navigator.Viewport.Width}x{map.Navigator.Viewport.Height}, resolution={map.Navigator.Viewport.Resolution}");
        FitToMission();
    }

    private void Render(MissionMapSnapshot snapshot)
    {
        markerLayer.Features = snapshot.Markers.Select(marker => CreatePointFeature(marker.Position, marker.Label,
            marker.Kind == MissionMapMarkerKind.Home ? Mapsui.Styles.Color.Green : Mapsui.Styles.Color.Blue)).ToArray();
        markerLayer.DataHasChanged();
        SetLine(routeLayer, snapshot.Route, Mapsui.Styles.Color.OrangeRed, 3);
        SetPointFeatures(vehicleLayer, [(viewModel.VehicleLatitude, viewModel.VehicleLongitude, "Vehicle")], Mapsui.Styles.Color.Red);
        mapView.ForceUpdate();
    }

    /// <summary>Applies a session-only viewport rotation in degrees.</summary>
    public void RotateTo(double degrees)
    {
        map.Navigator.RotateTo(degrees);
    }

    private IReadOnlyDictionary<PlanningLayerKind, MemoryLayer> CreatePlanningLayers()
    {
        return new Dictionary<PlanningLayerKind, MemoryLayer>
        {
            [PlanningLayerKind.Polygon] = NewLayer("Planning polygon"),
            [PlanningLayerKind.Measurement] = NewLayer("Measurement"),
            [PlanningLayerKind.Fence] = NewLayer("Fence"),
            [PlanningLayerKind.Rally] = NewLayer("Rally"),
            [PlanningLayerKind.Poi] = NewLayer("POI"),
            [PlanningLayerKind.Imported] = NewLayer("Imported"),
            [PlanningLayerKind.Survey] = NewLayer("Survey"),
            [PlanningLayerKind.TrackerHome] = NewLayer("Tracker home")
        };
    }

    private void RenderPlanningOverlays(MissionPlanningOverlaySnapshot snapshot)
    {
        SetLine(planningLayers[PlanningLayerKind.Polygon], snapshot.DrawnPolygon, Mapsui.Styles.Color.DeepSkyBlue, 3, snapshot.DrawnPolygon.Count >= 3);
        SetLine(planningLayers[PlanningLayerKind.Measurement], snapshot.TemporaryMeasurement, Mapsui.Styles.Color.Gold, 2);
        SetLine(planningLayers[PlanningLayerKind.Fence], snapshot.FencePreview, Mapsui.Styles.Color.Red, 3, snapshot.FencePreview.Count >= 3);
        SetLine(planningLayers[PlanningLayerKind.Rally], snapshot.RallyPoints, Mapsui.Styles.Color.Orange, 3);
        SetLine(planningLayers[PlanningLayerKind.Poi], snapshot.PoiItems, Mapsui.Styles.Color.Purple, 3);
        SetLine(planningLayers[PlanningLayerKind.Imported], snapshot.ImportedOverlays.SelectMany(x => x.Positions).ToArray(), Mapsui.Styles.Color.Cyan, 2);
        SetLine(planningLayers[PlanningLayerKind.Survey], snapshot.SurveyPreview, Mapsui.Styles.Color.Green, 2);
        SetLine(planningLayers[PlanningLayerKind.TrackerHome], snapshot.TrackerHome is { } home ? [home] : [], Mapsui.Styles.Color.White, 3);
        mapView.ForceUpdate();
    }

    private static MemoryLayer NewLayer(string name) => new() { Name = name, Features = [] };

    private static PointFeature CreatePointFeature(GeoPosition position, string label, Mapsui.Styles.Color color)
    {
        var (x, y) = SphericalMercator.FromLonLat(position.LongitudeDegrees, position.LatitudeDegrees);
        var feature = new PointFeature(new MPoint(x, y));
        feature.Styles.Add(new SymbolStyle { Fill = new Brush(color), Outline = new Pen(Mapsui.Styles.Color.White, 1), SymbolScale = 0.7 });
        feature.Styles.Add(new LabelStyle { Text = label, Offset = new Offset(0, 18) });
        return feature;
    }

    private static void SetPointFeatures(MemoryLayer layer, IEnumerable<(double Latitude, double Longitude, string Label)> points,
        Mapsui.Styles.Color color)
    {
        layer.Features = points.Select(x => CreatePointFeature(new GeoPosition(x.Latitude, x.Longitude), x.Label, color)).ToArray();
        layer.DataHasChanged();
    }

    private static void SetLine(MemoryLayer layer, IReadOnlyList<GeoPosition> positions, Mapsui.Styles.Color color,
        float width, bool close = false)
    {
        var coordinates = positions.Select(position =>
        {
            var (x, y) = SphericalMercator.FromLonLat(position.LongitudeDegrees, position.LatitudeDegrees);
            return new Coordinate(x, y);
        }).ToList();
        if (close && coordinates.Count > 0) coordinates.Add(coordinates[0]);
        if (coordinates.Count < 2) layer.Features = [];
        else
        {
            var feature = new GeometryFeature(new LineString(coordinates.ToArray()));
            feature.Styles.Add(new VectorStyle { Line = new Pen(color, width) });
            layer.Features = [feature];
        }
        layer.DataHasChanged();
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
            Debug.WriteLine($"[MissionMap] Fitting single position: latitude={position.LatitudeDegrees}, longitude={position.LongitudeDegrees}");
            CenterOn(position.LatitudeDegrees, position.LongitudeDegrees, true);
            return;
        }

        if (snapshot.Bounds is not { } bounds)
        {
            Debug.WriteLine("[MissionMap] Fit ignored because the mission snapshot has no bounds.");
            return;
        }

        var (minX, minY) = SphericalMercator.FromLonLat(bounds.West, bounds.South);
        var (maxX, maxY) = SphericalMercator.FromLonLat(bounds.East, bounds.North);
        Debug.WriteLine($"[MissionMap] Fitting projected bounds: minX={minX}, minY={minY}, maxX={maxX}, maxY={maxY}");
        Navigate(() =>
        {
            map.Navigator.ZoomToBox(new MRect(minX, minY, maxX, maxY));
            Debug.WriteLine($"[MissionMap] Fit applied: center=({map.Navigator.Viewport.CenterX},{map.Navigator.Viewport.CenterY}), resolution={map.Navigator.Viewport.Resolution}");
        });
    }

    /// <summary>
    /// Runs viewport changes on the map UI thread and lets Mapsui retain changes requested
    /// before its native control has initialized a usable viewport.
    /// </summary>
    private void Navigate(Action navigation)
    {
        if (disposed)
        {
            return;
        }

        if (!mapView.Dispatcher.CheckAccess())
        {
            mapView.Dispatcher.Post(() => Navigate(navigation));
            return;
        }

        var viewport = map.Navigator.Viewport;
        if (viewport.Width <= 0 || viewport.Height <= 0)
        {
            // Only the latest requested viewport is relevant (for example, fitting a newly
            // loaded mission should supersede the initial geolocation request).
            pendingNavigation = navigation;
            Debug.WriteLine($"[MissionMap] Navigation queued because viewport is {viewport.Width}x{viewport.Height}.");
            return;
        }

        pendingNavigation = null;
        navigation();
    }

    private void OnMapViewSizeChanged(object? sender, EventArgs args)
    {
        RefreshBasemapForCurrentViewport();

        if (pendingNavigation is not { } navigation || disposed)
        {
            return;
        }

        var viewport = map.Navigator.Viewport;
        if (viewport.Width <= 0 || viewport.Height <= 0)
        {
            return;
        }

        pendingNavigation = null;
        navigation();
    }

    private void RefreshBasemapForCurrentViewport()
    {
        if (!basemapRefreshPending || disposed || !isActive)
        {
            return;
        }

        var viewport = map.Navigator.Viewport;
        if (viewport.Width <= 0 || viewport.Height <= 0)
        {
            return;
        }

        basemapRefreshPending = false;
        map.Refresh(ChangeType.Discrete);
    }

    private async Task SwitchSourceAsync(string sourceId, CancellationToken cancellationToken)
    {
        if (!isActive || disposed)
        {
            return;
        }

        try
        {
            var result = await basemapController.SwitchAsync(sourceId, cancellationToken);
            if (result.IsSuccess && isActive && !disposed)
            {
                basemapRefreshPending = true;
                await new AvaloniaMapUiDispatcher(mapView.Dispatcher).InvokeAsync(
                    RefreshBasemapForCurrentViewport, cancellationToken);
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
