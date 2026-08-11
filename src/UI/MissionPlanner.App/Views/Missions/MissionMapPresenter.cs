using System.ComponentModel;
using System.Diagnostics;
using BruTile.Predefined;
using Mapsui;
using Mapsui.Extensions;
using Mapsui.Layers;
using Mapsui.Projections;
using Mapsui.Tiling;
using Mapsui.Tiling.Layers;
using Mapsui.UI.Maui;
using MissionPlanner.Core.ConfigTuning.Planner;

namespace MissionPlanner.App.Views.Missions;

/// <summary>Owns Mapsui rendering and navigation for a mission-map view.</summary>
internal sealed class MissionMapPresenter : IDisposable
{
    private const double WebMercatorInitialResolution = 156543.03392804097;
    private static readonly long PointerUpdateInterval = Stopwatch.Frequency / 30;
    private readonly MapView mapView;
    private readonly MissionItemListViewModel viewModel;
    private readonly IPlannerSettingsService plannerSettings;
    private readonly Mapsui.Map map = new();
    private readonly Pin vehiclePin;
    private readonly List<Pin> missionPins = [];
    private Polyline routeLine;
    private long lastPointerUpdate;
    private bool disposed;

    /// <summary>Initializes a presenter for a map view and shared mission editor.</summary>
    public MissionMapPresenter(MapView mapView, MissionItemListViewModel viewModel, IPlannerSettingsService plannerSettings)
    {
        this.mapView = mapView;
        this.viewModel = viewModel;
        this.plannerSettings = plannerSettings;
        map.Layers.Add(CreateTileLayer(viewModel.SelectedMapType));
        mapView.Map = map;

        vehiclePin = new Pin(mapView)
        {
            Label = "Vehicle",
            Type = PinType.Pin,
            Position = new Position(viewModel.VehicleLatitude, viewModel.VehicleLongitude)
        };
        mapView.Pins.Add(vehiclePin);
        routeLine = new Polyline { StrokeColor = Colors.OrangeRed, StrokeWidth = 3 };
        mapView.Drawables.Add(routeLine);
        viewModel.PropertyChanged += OnViewModelPropertyChanged;
        viewModel.FitToMissionRequested += OnFitToMissionRequested;
        Render(viewModel.MapSnapshot);
    }

    /// <summary>Forwards a geographic primary click to the mission editor.</summary>
    public void HandleMapClick(double latitude, double longitude) =>
        viewModel.HandleMapClick(latitude, longitude);

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
        viewModel.SetPointerPosition(latitude, longitude);
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
    public void ZoomIn() => map.Navigator.ZoomIn();

    /// <summary>Zooms the map out by one navigator step.</summary>
    public void ZoomOut() => map.Navigator.ZoomOut();

    /// <summary>Centers and zooms the map on the current vehicle position.</summary>
    public void ZoomToVehicle() => CenterOn(viewModel.VehicleLatitude, viewModel.VehicleLongitude, true);

    /// <summary>Toggles automatic map following of the current vehicle.</summary>
    public void ToggleFollowVehicle() => viewModel.FollowVehicle = !viewModel.FollowVehicle;

    /// <inheritdoc />
    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        viewModel.FitToMissionRequested -= OnFitToMissionRequested;
        foreach (var pin in missionPins)
        {
            mapView.Pins.Remove(pin);
        }

        missionPins.Clear();
        mapView.Drawables.Remove(routeLine);
    }

    private double DefaultZoomResolution =>
        WebMercatorInitialResolution / Math.Pow(2, plannerSettings.Current.Map.DefaultZoom);

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName is nameof(MissionItemListViewModel.VehicleLatitude) or nameof(MissionItemListViewModel.VehicleLongitude))
        {
            var position = new Position(viewModel.VehicleLatitude, viewModel.VehicleLongitude);
            vehiclePin.Position = position;
            if (viewModel.FollowVehicle)
            {
                CenterOn(position.Latitude, position.Longitude);
            }
        }
        else if (args.PropertyName == nameof(MissionItemListViewModel.SelectedMapType))
        {
            ApplyMapType(viewModel.SelectedMapType);
        }
        else if (args.PropertyName == nameof(MissionItemListViewModel.MapSnapshot))
        {
            Render(viewModel.MapSnapshot);
        }
    }

    private void OnFitToMissionRequested(object? sender, EventArgs args) => FitToMission();

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

    private void ApplyMapType(string mapType)
    {
        var previous = map.Layers.FirstOrDefault();
        map.Layers.Insert(0, CreateTileLayer(mapType));
        if (previous is not null)
        {
            map.Layers.Remove(previous);
        }
    }

    private static ILayer CreateTileLayer(string mapType) => mapType switch
    {
        "Esri World Topo" => new TileLayer(KnownTileSources.Create(KnownTileSource.EsriWorldTopo)),
        "Esri World Physical" => new TileLayer(KnownTileSources.Create(KnownTileSource.EsriWorldPhysical)),
        "Esri Shaded Relief" => new TileLayer(KnownTileSources.Create(KnownTileSource.EsriWorldShadedRelief)),
        "Esri Dark Gray" => new TileLayer(KnownTileSources.Create(KnownTileSource.EsriWorldDarkGrayBase)),
        _ => OpenStreetMap.CreateTileLayer()
    };
}
