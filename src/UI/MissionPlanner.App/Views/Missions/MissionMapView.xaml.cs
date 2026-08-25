using System.Diagnostics;
using Mapsui;
using Mapsui.UI.Maui;
using MissionPlanner.Library.Factory.Domain.Abstractions;

namespace MissionPlanner.App.Views.Missions;

/// <summary>
/// Shared mission-map editor control. Native map events remain at the view boundary while
/// <see cref="MissionMapPresenter"/> owns Mapsui rendering and navigation.
/// </summary>
public partial class MissionMapView : ContentView, IDisposable
{
    private MissionMapViewModel? viewModel;
    private readonly IDomainFactory domainFactory;
    private MissionMapPresenter? presenter;
    private bool disposed;
    private bool isActive;
    private bool usingCustomPosition;

    /// <summary>Initializes a new instance of the <see cref="MissionMapView"/> class.</summary>
    public MissionMapView(IDomainFactory domainFactory)
    {
        InitializeComponent();
        this.domainFactory = domainFactory;
    }

    /// <summary>
    /// Activates the view with the specified view model.
    /// </summary>
    /// <param name="vModel">The view model to associate with the view.</param>
    public async Task ActivateAsync(MissionMapViewModel vModel)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (isActive)
        {
            return;
        }
        isActive = true;
        this.viewModel = vModel;
        this.MissionMap.MapClicked += OnMapClicked;
        this.MissionMap.MapPointerMoved += OnMapPointerMoved;
        this.viewModel.MapRotationRequested += OnMapRotationRequested;
        this.viewModel.MapCenterRequested += OnMapCenterRequested;
        this.BindingContext = this.viewModel;
        presenter ??= domainFactory.Create<MissionMapPresenter, MapView, MissionMapViewModel>(MissionMap, this.viewModel);
        await presenter.ActivateAsync();
        if (this.viewModel is not { VehicleLatitude: 0, VehicleLongitude: 0 })
        {
            return;
        }
        await CenterOnMyLocationAsync();
        usingCustomPosition = true;
    }

    /// <summary>
    /// Deactivates the view and releases resources.
    /// </summary>
    public void Deactivate()
    {
        if (disposed)
        {
            return;
        }

        if (!isActive)
        {
            return;
        }

        isActive = false;
        MissionMap.MapClicked -= OnMapClicked;
        MissionMap.MapPointerMoved -= OnMapPointerMoved;
        viewModel?.MapRotationRequested -= OnMapRotationRequested;
        viewModel?.MapCenterRequested -= OnMapCenterRequested;
        presenter?.Deactivate();
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
        presenter = null;
        viewModel = null;
        BindingContext = null;
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
        if (usingCustomPosition)
        {
            return;
        }

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
