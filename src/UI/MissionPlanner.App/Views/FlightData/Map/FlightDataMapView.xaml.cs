using System.ComponentModel;
using Mapsui;
using Mapsui.Tiling;
using Mapsui.UI.Maui;
using MissionPlanner.App.Configuration;

namespace MissionPlanner.App.Views.FlightData.Map;

/// <summary>
/// Represents the view for displaying flight data on a map.
/// </summary>
public partial class FlightDataMapView : ContentView
{
    private readonly FlightDataMapViewModel viewModel;
    private readonly Pin vehiclePin;
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

        viewModel.PropertyChanged += ViewModel_PropertyChanged;
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(FlightDataMapViewModel.VehicleLatitude) or nameof(FlightDataMapViewModel.VehicleLongitude))
        {
            Position position = new(viewModel.VehicleLatitude, viewModel.VehicleLongitude);
            vehiclePin.Position = position;

            var (x, y) = Mapsui.Projections.SphericalMercator.FromLonLat(position.Longitude, position.Latitude);
            map.Navigator.CenterOn(new MPoint(x, y));
        }
    }
}
