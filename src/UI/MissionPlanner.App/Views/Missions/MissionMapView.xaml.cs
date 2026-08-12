using System.Diagnostics;
using Mapsui;
using Mapsui.UI.Maui;
using MissionPlanner.Library.Factory.Domain.Abstractions;

namespace MissionPlanner.App.Views.Missions;

/// <summary>
/// Shared mission-map editor control. Native map events remain at the view boundary while
/// <see cref="MissionMapPresenter"/> owns Mapsui rendering and navigation.
/// </summary>
public partial class MissionMapView : ContentView, IDisposable //ExtendedContentView<MissionMapViewModel>
{
    private MissionMapViewModel? ViewModel;
    private readonly IDomainFactory domainFactory;
    private MissionMapPresenter? presenter;
    private bool disposed;

    /// <summary>Initializes a new instance of the <see cref="MissionMapView"/> class.</summary>
    public MissionMapView(IDomainFactory domainFactory)
    {
        this.domainFactory = domainFactory;
        InitializeComponent();
    }

    public async Task Activate(MissionMapViewModel viewModel)
    {
        ViewModel = viewModel;
        presenter = domainFactory.Create<MissionMapPresenter, MapView, MissionMapViewModel>(MissionMap, viewModel);
        MissionMap.MapClicked += OnMapClicked;
        MissionMap.MapPointerMoved += OnMapPointerMoved;
        viewModel.MapRotationRequested += OnMapRotationRequested;
        viewModel.MapCenterRequested += OnMapCenterRequested;
        //Loaded += OnLoaded;
        BindingContext = viewModel;
        // resolutions from the layers and can otherwise replace an earlier navigation.
        await presenter.ActivateAsync();
        if (ViewModel is not { VehicleLatitude: 0, VehicleLongitude: 0 })
        {
            return;
        }

        await CenterOnMyLocationAsync();
    }

    public void Deactivate()
    {
        if (disposed)
        {
            return;
        }

        presenter?.Deactivate();
        MissionMap.MapClicked -= OnMapClicked;
        MissionMap.MapPointerMoved -= OnMapPointerMoved;
        ViewModel?.MapRotationRequested -= OnMapRotationRequested;
        ViewModel?.MapCenterRequested -= OnMapCenterRequested;
        ViewModel = null;
        BindingContext = null;
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
        presenter?.Dispose();
        ViewModel?.Dispose();
        ViewModel = null;
        presenter = null;
        BindingContext = null;
        ViewModel = null;
    }

    private void OnMapClicked(object? sender, MapClickedEventArgs args)
    {
        presenter?.HandleMapClick(args.Point.Latitude, args.Point.Longitude);
    }

    private void OnMapPointerMoved(object? sender, MapEventArgs args)
    {
        presenter?.UpdatePointerPosition(args.ScreenPosition.X, args.ScreenPosition.Y);
    }

    private void OnMapRotationRequested(object? sender, double degrees)
    {
        presenter?.RotateTo(degrees);
    }

    private void OnMapCenterRequested(object? sender, MissionPlanner.Core.Missions.Models.GeoPosition position)
    {
        presenter?.CenterOn(position.LatitudeDegrees, position.LongitudeDegrees, false);
    }

    private async Task CenterOnMyLocationAsync()
    {
        try
        {
            var location = await Geolocation.Default.GetLastKnownLocationAsync()
                           ?? await Geolocation.Default.GetLocationAsync(
                               new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(10)));
            if (location is not null && !disposed)
            {
                presenter?.CenterOn(location.Latitude, location.Longitude, true);
            }
        }
        catch (Exception ex)
        {
            // Location permission missing or no provider available; retain the current viewport.
            Debug.Print(ex.Message);
            Debug.Print("On Windows: Ensure geolocation service is running");
        }
    }

    private void OnZoomInClicked(object? sender, EventArgs args)
    {
        presenter?.ZoomIn();
    }

    private void OnZoomOutClicked(object? sender, EventArgs args)
    {
        presenter?.ZoomOut();
    }

    private void OnZoomToVehicleClicked(object? sender, EventArgs args)
    {
        presenter?.ZoomToVehicle();
    }

    private async void OnCenterOnMyLocationClicked(object? sender, EventArgs args)
    {
        await CenterOnMyLocationAsync();
    }

    private void OnToggleFollowVehicleClicked(object? sender, EventArgs args)
    {
        presenter?.ToggleFollowVehicle();
    }

    private void OnAttributionClicked(object? sender, EventArgs args)
    {
        presenter?.ToggleAttribution();
    }
}
