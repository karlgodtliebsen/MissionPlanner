using System.ComponentModel;
using BruTile.Predefined;
using Mapsui;
using Mapsui.Extensions;
using Mapsui.Layers;
using Mapsui.Projections;
using Mapsui.Tiling;
using Mapsui.Tiling.Layers;
using Mapsui.UI.Maui;
using MissionPlanner.App.Configuration;

namespace MissionPlanner.App.Views.Missions;

/// <summary>
/// Shared mission map editor control. Renders the mission plan (pins + route) on a Mapsui map and
/// hosts the right-click context menu mirroring the classic MissionPlanner flight planner menu.
/// Bound to the singleton <see cref="MissionMapViewModel"/>, so every instance edits the same plan.
/// </summary>
public partial class MissionMapView : ContentView, IDisposable
{
    /// <summary>Resolution (meters/pixel at the equator) used when zooming to a point; roughly street level.</summary>
    private const double DefaultZoomResolution = 4.78;

    private readonly MissionMapViewModel viewModel;
    private readonly Pin vehiclePin;
    private Polyline routeLine;
    private readonly List<Pin> missionPins = [];
    private readonly Mapsui.Map map;

    /// <summary>
    /// Initializes a new instance of the <see cref="MissionMapView"/> class.
    /// </summary>
    public MissionMapView()
    {
        InitializeComponent();
        viewModel = ServiceHelper.GetRequiredService<MissionMapViewModel>();
        BindingContext = viewModel;
        map = new Mapsui.Map();
        map.Layers.Add(CreateTileLayer(viewModel.SelectedMapType));
        MissionMap.Map = map;

        vehiclePin = new Pin(MissionMap) { Label = "Vehicle", Type = PinType.Pin, Position = new Position(viewModel.VehicleLatitude, viewModel.VehicleLongitude) };
        MissionMap.Pins.Add(vehiclePin);

        routeLine = new Polyline { StrokeColor = Colors.OrangeRed, StrokeWidth = 3 };
        MissionMap.Drawables.Add(routeLine);

        MissionMap.MapClicked += OnMapClicked;

        var pointer = new PointerGestureRecognizer();
        pointer.PointerMoved += OnPointerMoved;
        GestureRecognizers.Add(pointer);

        viewModel.PropertyChanged += ViewModel_PropertyChanged;
        viewModel.MissionChanged += OnMissionChanged;
        Loaded += OnFirstLoaded;

        RedrawMission();
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MissionMapViewModel.VehicleLatitude) or nameof(MissionMapViewModel.VehicleLongitude))
        {
            Position position = new(viewModel.VehicleLatitude, viewModel.VehicleLongitude);
            vehiclePin.Position = position;

            if (viewModel.FollowVehicle)
            {
                CenterMap(position.Latitude, position.Longitude);
            }
        }
        else if (e.PropertyName is nameof(MissionMapViewModel.HomePosition))
        {
            RedrawMission();
        }
        else if (e.PropertyName is nameof(MissionMapViewModel.SelectedMapType))
        {
            ApplyMapType(viewModel.SelectedMapType);
        }
    }

    private void OnMissionChanged(object? sender, EventArgs e)
    {
        RedrawMission();
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

    private static ILayer CreateTileLayer(string mapType)
    {
        return mapType switch
        {
            "Esri World Topo" => new TileLayer(KnownTileSources.Create(KnownTileSource.EsriWorldTopo)),
            "Esri World Physical" => new TileLayer(KnownTileSources.Create(KnownTileSource.EsriWorldPhysical)),
            "Esri Shaded Relief" => new TileLayer(KnownTileSources.Create(KnownTileSource.EsriWorldShadedRelief)),
            "Esri Dark Gray" => new TileLayer(KnownTileSources.Create(KnownTileSource.EsriWorldDarkGrayBase)),
            var _ => OpenStreetMap.CreateTileLayer()
        };
    }

    private void RedrawMission()
    {
        foreach (var pin in missionPins)
        {
            MissionMap.Pins.Remove(pin);
        }

        missionPins.Clear();

        // The MapView only rebuilds its drawables layer on collection changes, so mutating
        // the existing Polyline's positions never repaints. Replace the instance instead.
        var newRouteLine = new Polyline { StrokeColor = Colors.OrangeRed, StrokeWidth = 3 };

        if (viewModel.HomePosition is { } home)
        {
            AddMissionPin("H: Home", home.LatitudeDegrees, home.LongitudeDegrees, Colors.Green);
            newRouteLine.Positions.Add(new Position(home.LatitudeDegrees, home.LongitudeDegrees));
        }

        foreach (var item in viewModel.Mission.Items)
        {
            if (MissionMapViewModel.PositionOf(item) is not { } position)
            {
                continue;
            }

            AddMissionPin($"{item.Sequence + 1}: {item.Command}", position.LatitudeDegrees, position.LongitudeDegrees, Colors.DodgerBlue);
            newRouteLine.Positions.Add(new Position(position.LatitudeDegrees, position.LongitudeDegrees));
        }

        MissionMap.Drawables.Remove(routeLine);
        MissionMap.Drawables.Add(newRouteLine);
        routeLine = newRouteLine;

        MissionMap.RefreshGraphics();
    }

    private void AddMissionPin(string label, double latitude, double longitude, Color color)
    {
        var pin = new Pin(MissionMap)
        {
            Label = label,
            Type = PinType.Pin,
            Color = color,
            Scale = 0.7f,
            Position = new Position(latitude, longitude)
        };
        missionPins.Add(pin);
        MissionMap.Pins.Add(pin);
    }

    private void OnMapClicked(object? sender, MapClickedEventArgs e)
    {
        viewModel.SetContextPosition(e.Point.Latitude, e.Point.Longitude);
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (e.GetPosition(MissionMap) is not { } point)
        {
            return;
        }

        var viewport = map.Navigator.Viewport;
        if (viewport.Width <= 0 || viewport.Height <= 0)
        {
            return;
        }

        var world = viewport.ScreenToWorld(point.X, point.Y);
        var (longitude, latitude) = SphericalMercator.ToLonLat(world.X, world.Y);
        viewModel.SetContextPosition(latitude, longitude);
    }

    private async void OnFirstLoaded(object? sender, EventArgs e)
    {
        Loaded -= OnFirstLoaded;

        // Without a vehicle fix, start the map near the user's own location.
        if (viewModel.VehicleLatitude == 0 && viewModel.VehicleLongitude == 0)
        {
            await CenterOnMyLocationAsync();
        }
    }

    private async Task CenterOnMyLocationAsync()
    {
        try
        {
            var location = await Geolocation.Default.GetLastKnownLocationAsync()
                           ?? await Geolocation.Default.GetLocationAsync(new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(10)));
            if (location is not null)
            {
                CenterMap(location.Latitude, location.Longitude, DefaultZoomResolution);
            }
        }
        catch (Exception)
        {
            // Location permission missing or no provider available; the map keeps its current view.
        }
    }

    private void CenterMap(double latitude, double longitude, double? resolution = null)
    {
        var (x, y) = SphericalMercator.FromLonLat(longitude, latitude);
        if (resolution is { } r)
        {
            map.Navigator.CenterOnAndZoomTo(new MPoint(x, y), r);
        }
        else
        {
            map.Navigator.CenterOn(new MPoint(x, y));
        }
    }

    private void OnZoomInClicked(object? sender, EventArgs e)
    {
        map.Navigator.ZoomIn();
    }

    private void OnZoomOutClicked(object? sender, EventArgs e)
    {
        map.Navigator.ZoomOut();
    }

    private void OnZoomToVehicleClicked(object? sender, EventArgs e)
    {
        CenterMap(viewModel.VehicleLatitude, viewModel.VehicleLongitude, DefaultZoomResolution);
    }

    private async void OnCenterOnMyLocationClicked(object? sender, EventArgs e)
    {
        await CenterOnMyLocationAsync();
    }

    private void OnToggleFollowVehicleClicked(object? sender, EventArgs e)
    {
        viewModel.FollowVehicle = !viewModel.FollowVehicle;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        viewModel.PropertyChanged -= ViewModel_PropertyChanged;
        viewModel.MissionChanged -= OnMissionChanged;
        MissionMap.MapClicked -= OnMapClicked;
    }
}
