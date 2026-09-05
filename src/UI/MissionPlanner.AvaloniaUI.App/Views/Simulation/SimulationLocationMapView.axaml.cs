using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
using BruTile.Predefined;
using Mapsui;
using Mapsui.Layers;
using Mapsui.Projections;
using Mapsui.Styles;
using Mapsui.Tiling;
using Mapsui.UI.Avalonia;
using MissionPlanner.AvaloniaUI.App.Services;
using MissionPlanner.AvaloniaUI.App.Utilities;

namespace MissionPlanner.AvaloniaUI.App.Views.Simulation;

/// <summary>Hosts the Mapsui location picker used to choose the SITL start position.</summary>
public partial class SimulationLocationMapView : UserControl, IDisposable
{
    private readonly IPlatformLocationService locationService;
    private readonly Map map = new();
    private readonly MemoryLayer markerLayer = new() { Name = "SITL start", Features = [] };
    private SimulationViewModel? viewModel;
    private bool disposed;

    /// <summary>Initializes the simulation location map.</summary>
    public SimulationLocationMapView()
    {
        InitializeComponent();
        locationService = ServiceHelper.GetRequiredService<IPlatformLocationService>();
        map.Layers.Add(OpenStreetMap.CreateTileLayer());
        map.Layers.Add(markerLayer);
        LocationMap.Map = map;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        DataContextChanged += OnDataContextChanged;
    }

    /// <summary>Centers the map on the current platform location without changing the selected start position.</summary>
    public async Task CenterOnMyLocationAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        var location = await locationService.GetLocationAsync(cancellationToken);
        if (location is not null && !disposed)
        {
            CenterOn(location.Value.LatitudeDegrees, location.Value.LongitudeDegrees);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        DetachViewModel();
        LocationMap.MapTapped -= OnMapTapped;
        Loaded -= OnLoaded;
        Unloaded -= OnUnloaded;
        DataContextChanged -= OnDataContextChanged;
        LocationMap.Map = null;
    }

    private async void OnLoaded(object? sender, RoutedEventArgs args)
    {
        LocationMap.MapTapped -= OnMapTapped;
        LocationMap.MapTapped += OnMapTapped;
        AttachViewModel();
        Redraw(center: true);
        try
        {
            await CenterOnMyLocationAsync();
        }
        catch (OperationCanceledException)
        {
            // The view was unloaded while the platform location lookup was running.
        }
    }

    private void OnUnloaded(object? sender, RoutedEventArgs args)
    {
        LocationMap.MapTapped -= OnMapTapped;
        DetachViewModel();
    }

    private void OnDataContextChanged(object? sender, EventArgs args)
    {
        AttachViewModel();
        Redraw(center: true);
    }

    private void AttachViewModel()
    {
        DetachViewModel();
        viewModel = DataContext as SimulationViewModel;
        if (viewModel is not null)
        {
            viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }
    }

    private void DetachViewModel()
    {
        if (viewModel is not null)
        {
            viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            viewModel = null;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName is nameof(SimulationViewModel.Latitude) or nameof(SimulationViewModel.Longitude))
        {
            Redraw(center: false);
        }
    }

    private void OnMapTapped(object? sender, MapEventArgs args)
    {
        var (longitude, latitude) = SphericalMercator.ToLonLat(args.WorldPosition.X, args.WorldPosition.Y);
        viewModel?.HandleMapLocationClick(latitude, longitude);
    }

    private void Redraw(bool center)
    {
        if (viewModel is null ||
            viewModel.Latitude is < -90 or > 90 ||
            viewModel.Longitude is < -180 or > 180)
        {
            markerLayer.Features = [];
            markerLayer.DataHasChanged();
            return;
        }

        var (x, y) = SphericalMercator.FromLonLat(viewModel.Longitude, viewModel.Latitude);
        var marker = new PointFeature(new MPoint(x, y));
        marker.Styles.Add(new SymbolStyle
        {
            Fill = new Brush(Color.DodgerBlue),
            Outline = new Pen(Color.White, 1),
            SymbolScale = 0.8
        });
        markerLayer.Features = [marker];
        markerLayer.DataHasChanged();
        if (center)
        {
            CenterOn(viewModel.Latitude, viewModel.Longitude);
        }
    }

    private void CenterOn(double latitude, double longitude)
    {
        var (x, y) = SphericalMercator.FromLonLat(longitude, latitude);
        map.Navigator.CenterOnAndZoomTo(new MPoint(x, y), 4.78);
    }
}
