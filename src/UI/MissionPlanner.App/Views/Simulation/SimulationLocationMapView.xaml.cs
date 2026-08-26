using System.ComponentModel;
using System.Diagnostics;
using BruTile.Predefined;
using Mapsui;
using Mapsui.Projections;
using Mapsui.Tiling;
using Mapsui.UI.Maui;

namespace MissionPlanner.App.Views.Simulation;

/// <summary>Provides unavoidable map-click integration for selecting a simulator start location.</summary>
public partial class SimulationLocationMapView : ContentView, IDisposable
{
    private readonly Mapsui.Map map;
    private readonly List<Pin> pins = [];
    private SimulationViewModel? viewModel;
    private bool disposed;

    /// <summary>Initializes the location-selection map.</summary>
    public SimulationLocationMapView()
    {
        InitializeComponent();
        map = new Mapsui.Map();
        map.Layers.Add(OpenStreetMap.CreateTileLayer());
        LocationMap.Map = map;
        LocationMap.MapClicked += OnMapClicked;
        BindingContextChanged += OnBindingContextChanged;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        LocationMap.MapClicked -= OnMapClicked;
        BindingContextChanged -= OnBindingContextChanged;
        if (viewModel is not null)
        {
            viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }
    }

    /// <summary>Centers the map on the device's current location without changing the SITL start position.</summary>
    public async Task CenterOnMyLocationAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        try
        {
            var location = await Geolocation.Default.GetLastKnownLocationAsync()
                           ?? await Geolocation.Default.GetLocationAsync(
                               new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(10)),
                               cancellationToken);
            if (location is null || disposed)
            {
                return;
            }

            var (x, y) = SphericalMercator.FromLonLat(location.Longitude, location.Latitude);
            map.Navigator.CenterOnAndZoomTo(new MPoint(x, y), 4.78);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            Debug.WriteLine($"Unable to center the simulation map on the current location: {exception}");
        }
    }

    private void OnBindingContextChanged(object? sender, EventArgs args)
    {
        if (viewModel is not null)
        {
            viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        viewModel = BindingContext as SimulationViewModel;
        if (viewModel is not null)
        {
            viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }

        Redraw(center: true);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName is nameof(SimulationViewModel.Latitude) or nameof(SimulationViewModel.Longitude))
        {
            Redraw(center: false);
        }
    }

    private void OnMapClicked(object? sender, MapClickedEventArgs args)
    {
        viewModel?.HandleMapLocationClick(args.Point.Latitude, args.Point.Longitude);
        Redraw(center: false);
    }

    private void Redraw(bool center)
    {
        foreach (var existingPin in pins)
        {
            LocationMap.Pins.Remove(existingPin);
        }

        pins.Clear();
        if (viewModel is null ||
            viewModel.Latitude is < -90 or > 90 ||
            viewModel.Longitude is < -180 or > 180)
        {
            return;
        }

        var position = new Position(viewModel.Latitude, viewModel.Longitude);
        var pin = new Pin(LocationMap)
        {
            Label = "SITL start",
            Type = PinType.Pin,
            Color = Colors.DodgerBlue,
            Position = position
        };
        pins.Add(pin);
        LocationMap.Pins.Add(pin);
        LocationMap.RefreshGraphics();
        if (center)
        {
            var (x, y) = SphericalMercator.FromLonLat(position.Longitude, position.Latitude);
            map.Navigator.CenterOnAndZoomTo(new MPoint(x, y), 4.78);
        }
    }
}
