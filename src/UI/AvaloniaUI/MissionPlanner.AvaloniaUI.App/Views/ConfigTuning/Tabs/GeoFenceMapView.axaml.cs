using AsyncAwaitBestPractices;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Mapsui;
using Mapsui.Layers;
using Mapsui.Nts;
using Mapsui.Projections;
using Mapsui.Styles;
using Mapsui.UI.Avalonia;
using Microsoft.Extensions.Logging;
using MissionPlanner.AvaloniaUI.App.Maps;
using MissionPlanner.AvaloniaUI.App.Services;
using MissionPlanner.AvaloniaUI.App.Utilities;
using MissionPlanner.Core.ConfigTuning.Fences;
using MissionPlanner.Core.ConfigTuning.Planner;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Library;
using MissionPlanner.Maps.Attribution;
using MissionPlanner.Maps.Sources;
using MissionPlanner.Maps.Terrain;
using NetTopologySuite.Geometries;

namespace MissionPlanner.AvaloniaUI.App.Views.ConfigTuning.Tabs;

public partial class GeoFenceMapView : UserControlViewBase<GeoFenceTabViewModel>, IDisposable
{
    private const double WebMercatorInitialResolution = 156543.03392804097;
    private readonly Mapsui.Map map = new();
    private readonly MemoryLayer geometryLayer = new() { Name = "Fence geometry", Features = [] };
    private readonly MapBasemapController basemapController;
    private readonly IPlannerSettingsService settingsService;
    private readonly IActiveVehicleContext activeVehicle;
    private readonly IPlatformLocationService locationService;
    private readonly ITerrainElevationService terrainElevationService;
    private readonly IMapAttributionCoordinator attributionCoordinator;
    private CancellationTokenSource? mapLifecycleCancellation;
    private long pointerGeneration;
    private Action? pendingNavigation;
    private bool mapActive;
    private bool disposed;

    public GeoFenceMapView()
    {
        InitializeComponent();
        settingsService = ServiceHelper.GetRequiredService<IPlannerSettingsService>();
        activeVehicle = ServiceHelper.GetRequiredService<IActiveVehicleContext>();
        locationService = ServiceHelper.GetRequiredService<IPlatformLocationService>();
        terrainElevationService = ServiceHelper.GetRequiredService<ITerrainElevationService>();
        attributionCoordinator = ServiceHelper.GetRequiredService<IMapAttributionCoordinator>();
        basemapController = new MapBasemapController(
            map,
            ServiceHelper.GetRequiredService<IMapSourceResolver>(),
            ServiceHelper.GetRequiredService<IMapsuiBasemapFactory>(),
            new AvaloniaMapUiDispatcher(Dispatcher));
        map.Layers.Add(geometryLayer);
        FenceMap = this.FindControl<MapControl>("FenceMap");
        DomainException.ThrowIfNull(FenceMap, "The MapsUI FenceMap could not be loaded.");
        FenceMap.Map = map;
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        DeactivateMap();
        basemapController.Dispose();
    }

    /// <inheritdoc />
    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        if (disposed || mapActive)
        {
            return;
        }

        mapActive = true;
        FenceMap.MapTapped += OnMapTapped;
        FenceMap.MapPointerMoved += OnMapPointerMoved;
        attributionCoordinator.Changed += OnAttributionChanged;
        ViewModel.GeometryChanged += OnGeometryChanged;
        FenceMap.SizeChanged += OnMapLayoutChanged;
        FenceMap.LayoutUpdated += OnMapLayoutChanged;
        Redraw();
        mapLifecycleCancellation = new CancellationTokenSource();
        ActivateBasemapAsync(mapLifecycleCancellation.Token).SafeFireAndForget(OnBasemapActivationFailed);
    }

    /// <inheritdoc />
    protected override void OnUnloaded(RoutedEventArgs e)
    {
        DeactivateMap();
        base.OnUnloaded(e);
    }

    private async Task ActivateBasemapAsync(CancellationToken cancellationToken)
    {
        await basemapController.SwitchAsync(settingsService.Current.Map.SelectedSourceId, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        await attributionCoordinator.SetBasemapAsync(basemapController.CurrentResolvedSource, cancellationToken);
        await CenterInitiallyAsync(cancellationToken);
    }

    private void OnBasemapActivationFailed(Exception exception)
    {
        Logger.LogError(exception, "Could not activate the GeoFence basemap source {SourceId}.", settingsService.Current.Map.SelectedSourceId);
    }

    private void DeactivateMap()
    {
        if (!mapActive)
        {
            return;
        }

        mapActive = false;
        FenceMap.MapTapped -= OnMapTapped;
        FenceMap.MapPointerMoved -= OnMapPointerMoved;
        attributionCoordinator.Changed -= OnAttributionChanged;
        ViewModel.GeometryChanged -= OnGeometryChanged;
        FenceMap.SizeChanged -= OnMapLayoutChanged;
        FenceMap.LayoutUpdated -= OnMapLayoutChanged;
        pendingNavigation = null;
        mapLifecycleCancellation?.Cancel();
        mapLifecycleCancellation?.Dispose();
        mapLifecycleCancellation = null;
        pointerGeneration++;
    }

    private async Task CenterInitiallyAsync(CancellationToken cancellationToken)
    {
        var latitude = activeVehicle.State?.Position.LatitudeDegrees;
        var longitude = activeVehicle.State?.Position.LongitudeDegrees;
        if (IsValidPosition(latitude, longitude))
        {
            CenterOn(latitude!.Value, longitude!.Value);
            return;
        }

        var location = await locationService.GetLocationAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (location is not null && !disposed && mapActive)
        {
            CenterOn(location.Value.LatitudeDegrees, location.Value.LongitudeDegrees);
        }
    }

    private static bool IsValidPosition(double? latitude, double? longitude)
    {
        return latitude is >= -90 and <= 90 && longitude is >= -180 and <= 180 &&
               (latitude.Value != 0 || longitude.Value != 0);
    }

    private void CenterOn(double latitude, double longitude)
    {
        var (x, y) = SphericalMercator.FromLonLat(longitude, latitude);
        Navigate(() => map.Navigator.CenterOnAndZoomTo(
            new MPoint(x, y),
            WebMercatorInitialResolution / Math.Pow(2, settingsService.Current.Map.DefaultZoom)));
    }

    private void Navigate(Action navigation)
    {
        var viewport = map.Navigator.Viewport;
        if (viewport.Width <= 0 || viewport.Height <= 0)
        {
            pendingNavigation = navigation;
            return;
        }

        pendingNavigation = null;
        navigation();
    }

    private void OnMapLayoutChanged(object? sender, EventArgs e)
    {
        if (pendingNavigation is not { } navigation || disposed || !mapActive)
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

    private void OnZoomInClicked(object? sender, RoutedEventArgs e)
    {
        map.Navigator.ZoomIn();
    }

    private void OnZoomOutClicked(object? sender, RoutedEventArgs e)
    {
        map.Navigator.ZoomOut();
    }

    private async void OnCenterOnMyLocationClicked(object? sender, RoutedEventArgs e)
    {
        var cancellationToken = mapLifecycleCancellation?.Token ?? CancellationToken.None;
        try
        {
            var location = await locationService.GetLocationAsync(cancellationToken);
            if (location is { } position && mapActive && !disposed)
            {
                CenterOn(position.LatitudeDegrees, position.LongitudeDegrees);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The map was unloaded while the location request was active.
        }
    }

    private void OnResetNorthClicked(object? sender, RoutedEventArgs e)
    {
        map.Navigator.RotateTo(0);
    }

    private void OnGeometryChanged(object? sender, EventArgs e)
    {
        Redraw();
    }

    private void OnMapTapped(object? sender, MapEventArgs e)
    {
        var (longitude, latitude) = SphericalMercator.ToLonLat(e.WorldPosition.X, e.WorldPosition.Y);
        ViewModel?.HandleMapClick(latitude, longitude);
    }

    private void OnMapPointerMoved(object? sender, MapEventArgs e)
    {
        var (longitude, latitude) = SphericalMercator.ToLonLat(e.WorldPosition.X, e.WorldPosition.Y);
        ViewModel.SetPointerPosition(latitude, longitude);
        ViewModel.SetPointerElevationLoading();
        RequestPointerElevation(latitude, longitude);
    }

    private void RequestPointerElevation(double latitude, double longitude)
    {
        var generation = ++pointerGeneration;
        var cancellationToken = mapLifecycleCancellation?.Token ?? CancellationToken.None;
        UpdatePointerElevationAsync(latitude, longitude, generation, cancellationToken)
            .SafeFireAndForget(OnPointerElevationFailed);
    }

    private async Task UpdatePointerElevationAsync(
        double latitude,
        double longitude,
        long generation,
        CancellationToken cancellationToken)
    {
        try
        {
            // Use the generation as the pointer debounce. Cancelling a delay for every
            // mouse movement produces a stream of expected TaskCanceledExceptions in
            // the debugger even though no operation has actually failed.
            await Task.Delay(250, cancellationToken);
            if (generation != pointerGeneration || disposed || !mapActive)
            {
                return;
            }

            var result = await terrainElevationService.GetElevationAsync(latitude, longitude, cancellationToken);
            if (generation != pointerGeneration || disposed || !mapActive)
            {
                return;
            }

            await new AvaloniaMapUiDispatcher(Dispatcher).InvokeAsync(
                () => ViewModel.SetPointerElevation(result), cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The map was unloaded or disposed while a debounce/terrain lookup was active.
        }
    }

    private void OnPointerElevationFailed(Exception exception)
    {
        if (exception is not OperationCanceledException)
        {
            Logger.LogDebug(exception, "Could not resolve terrain altitude for the GeoFence map pointer.");
        }
    }

    private void OnAttributionChanged(object? sender, MapAttributionOverlayState state)
    {
        Dispatcher.Post(() => ViewModel.SetAttribution(state.DisplayText));
    }

    private void OnAttributionClicked(object? sender, RoutedEventArgs e)
    {
        attributionCoordinator.ToggleExpanded();
    }

    private void Redraw()
    {
        var features = new List<IFeature>();
        if (ViewModel.LocalPlan.ReturnPoint is { } returnPoint)
        {
            features.Add(Point(returnPoint.LatitudeDegrees, returnPoint.LongitudeDegrees, Mapsui.Styles.Color.Blue));
        }

        foreach (var area in ViewModel.LocalPlan.Areas)
        {
            var color = area.Kind is FenceAreaKind.PolygonInclusion or FenceAreaKind.CircleInclusion
                ? Mapsui.Styles.Color.Green : Mapsui.Styles.Color.OrangeRed;
            if (area.Kind is FenceAreaKind.PolygonInclusion or FenceAreaKind.PolygonExclusion)
            {
                features.Add(Line(area.Vertices.Select(v => (v.LatitudeDegrees, v.LongitudeDegrees)).ToList(), color, area.IsClosed));
            }
            else if (area.Center is { } center)
            {
                features.Add(Line(Circle(center.LatitudeDegrees, center.LongitudeDegrees, area.RadiusMeters), color, true));
            }
        }
        geometryLayer.Features = features;
        geometryLayer.DataHasChanged();
        FenceMap.ForceUpdate();
        var extent = geometryLayer.Extent;
        if (extent is not null)
        {
            Navigate(() => map.Navigator.ZoomToBox(extent));
        }
    }

    private static PointFeature Point(double latitude, double longitude, Mapsui.Styles.Color color)
    {
        var (x, y) = SphericalMercator.FromLonLat(longitude, latitude);
        var feature = new PointFeature(new MPoint(x, y));
        feature.Styles.Add(new SymbolStyle { Fill = new Brush(color), Outline = new Pen(Mapsui.Styles.Color.White, 1) });
        return feature;
    }

    private static GeometryFeature Line(IReadOnlyList<(double Latitude, double Longitude)> positions, Mapsui.Styles.Color color, bool close)
    {
        var coordinates = positions.Select(p => { var (x, y) = SphericalMercator.FromLonLat(p.Longitude, p.Latitude); return new Coordinate(x, y); }).ToList();
        if (close && coordinates.Count > 0)
        {
            coordinates.Add(coordinates[0]);
        }

        var feature = new GeometryFeature(new LineString(coordinates.ToArray()));
        feature.Styles.Add(new VectorStyle { Line = new Pen(color, 3) });
        return feature;
    }

    private static List<(double Latitude, double Longitude)> Circle(double latitude, double longitude, double radiusMeters)
    {
        const int segments = 48;
        const double metersPerDegree = 111_320;
        return Enumerable.Range(0, segments).Select(index =>
        {
            var angle = index * Math.Tau / segments;
            return (latitude + (Math.Sin(angle) * radiusMeters / metersPerDegree),
                longitude + (Math.Cos(angle) * radiusMeters / (metersPerDegree * Math.Max(.01, Math.Cos(latitude * Math.PI / 180)))));
        }).ToList();
    }
}
