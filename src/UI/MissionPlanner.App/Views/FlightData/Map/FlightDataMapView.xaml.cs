using System.ComponentModel;
using Mapsui;
using Mapsui.Extensions;
using Mapsui.Projections;
using Mapsui.Tiling;
using Mapsui.UI.Maui;
using MissionPlanner.App.Configuration;

namespace MissionPlanner.App.Views.FlightData.Map;

/// <summary>
/// Represents the view for displaying flight data on a map. The map doubles as the mission plan
/// editor: a right-click (context) menu mirrors the classic MissionPlanner flight planner menu.
/// </summary>
public partial class FlightDataMapView : ContentView, IDisposable
{
    /// <summary>Resolution (meters/pixel at the equator) used when zooming to a point; roughly street level.</summary>
    private const double DefaultZoomResolution = 4.78;

    private readonly FlightDataMapViewModel viewModel;
    private readonly Pin vehiclePin;
    private readonly Polyline routeLine;
    private readonly List<Pin> missionPins = [];
    private readonly Mapsui.Map map;

    /// <summary>
    /// Initializes a new instance of the <see cref="FlightDataMapView"/> class.
    /// </summary>
    public FlightDataMapView()
    {
        InitializeComponent();
        viewModel = ServiceHelper.GetRequiredService<FlightDataMapViewModel>();
        BindingContext = viewModel;
        map = new Mapsui.Map();
        map.Layers.Add(OpenStreetMap.CreateTileLayer());
        VehicleMapView.Map = map;

        vehiclePin = new Pin(VehicleMapView) { Label = "Vehicle", Type = PinType.Pin, Position = new Position(viewModel.VehicleLatitude, viewModel.VehicleLongitude) };
        VehicleMapView.Pins.Add(vehiclePin);

        routeLine = new Polyline { StrokeColor = Colors.OrangeRed, StrokeWidth = 3 };
        VehicleMapView.Drawables.Add(routeLine);

        VehicleMapView.MapClicked += OnMapClicked;

        var pointer = new PointerGestureRecognizer();
        pointer.PointerMoved += OnPointerMoved;
        GestureRecognizers.Add(pointer);

        viewModel.PropertyChanged += ViewModel_PropertyChanged;
        viewModel.MissionChanged += OnMissionChanged;
        Loaded += OnFirstLoaded;
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(FlightDataMapViewModel.VehicleLatitude) or nameof(FlightDataMapViewModel.VehicleLongitude))
        {
            Position position = new(viewModel.VehicleLatitude, viewModel.VehicleLongitude);
            vehiclePin.Position = position;

            if (viewModel.FollowVehicle)
            {
                CenterMap(position.Latitude, position.Longitude);
            }
        }
        else if (e.PropertyName is nameof(FlightDataMapViewModel.HomePosition))
        {
            RedrawMission();
        }
    }

    private void OnMissionChanged(object? sender, EventArgs e)
    {
        RedrawMission();
    }

    private void RedrawMission()
    {
        foreach (var pin in missionPins)
        {
            VehicleMapView.Pins.Remove(pin);
        }

        missionPins.Clear();
        routeLine.Positions.Clear();

        if (viewModel.HomePosition is { } home)
        {
            AddMissionPin("H: Home", home.LatitudeDegrees, home.LongitudeDegrees, Colors.Green);
            routeLine.Positions.Add(new Position(home.LatitudeDegrees, home.LongitudeDegrees));
        }

        foreach (var item in viewModel.Mission.Items)
        {
            if (FlightDataMapViewModel.PositionOf(item) is not { } position)
            {
                continue;
            }

            AddMissionPin($"{item.Sequence + 1}: {item.Command}", position.LatitudeDegrees, position.LongitudeDegrees, Colors.DodgerBlue);
            routeLine.Positions.Add(new Position(position.LatitudeDegrees, position.LongitudeDegrees));
        }

        VehicleMapView.RefreshGraphics();
    }

    private void AddMissionPin(string label, double latitude, double longitude, Color color)
    {
        var pin = new Pin(VehicleMapView)
        {
            Label = label,
            Type = PinType.Pin,
            Color = color,
            Scale = 0.7f,
            Position = new Position(latitude, longitude)
        };
        missionPins.Add(pin);
        VehicleMapView.Pins.Add(pin);
    }

    private void OnMapClicked(object? sender, MapClickedEventArgs e)
    {
        viewModel.SetContextPosition(e.Point.Latitude, e.Point.Longitude);
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (e.GetPosition(VehicleMapView) is not { } point)
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
        VehicleMapView.MapClicked -= OnMapClicked;
    }
}
