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
    private readonly SemaphoreSlim lifecycleGate = new(1, 1);
    private CancellationTokenSource? operationCancellation;
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
        await lifecycleGate.WaitAsync();
        try
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (isActive)
            {
                return;
            }

            operationCancellation = new CancellationTokenSource();
            var token = operationCancellation.Token;
            this.viewModel = vModel;
            MissionMap.MapClicked += OnMapClicked;
            MissionMap.MapPointerMoved += OnMapPointerMoved;
            this.viewModel.MapRotationRequested += OnMapRotationRequested;
            this.viewModel.MapCenterRequested += OnMapCenterRequested;
            BindingContext = this.viewModel;
            presenter ??= domainFactory.Create<MissionMapPresenter, MapView, MissionMapViewModel>(MissionMap, this.viewModel);
            try
            {
                await presenter.ActivateAsync(token);
                token.ThrowIfCancellationRequested();
                if (this.viewModel is { VehicleLatitude: 0, VehicleLongitude: 0 })
                {
                    await CenterOnMyLocationAsync(token);
                    token.ThrowIfCancellationRequested();
                    usingCustomPosition = true;
                }
                isActive = true;
            }
            catch
            {
                Deactivate();
                throw;
            }
        }
        finally
        {
            lifecycleGate.Release();
        }
    }

    /// <summary>
    /// Deactivates the view and releases resources.
    /// </summary>
    public async Task DeactivateAsync()
    {
        await lifecycleGate.WaitAsync();
        try
        {
            if (disposed || !isActive)
            {
                return;
            }

            Deactivate();
        }
        finally
        {
            lifecycleGate.Release();
        }
    }

    private void Deactivate()
    {
        if (disposed || !isActive)
        {
            return;
        }
        isActive = false;
        MissionMap.MapClicked -= OnMapClicked;
        MissionMap.MapPointerMoved -= OnMapPointerMoved;
        viewModel?.MapRotationRequested -= OnMapRotationRequested;
        viewModel?.MapCenterRequested -= OnMapCenterRequested;
        presenter?.Deactivate();
        operationCancellation?.Cancel();
        operationCancellation?.Dispose();
        operationCancellation = null;
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

    private async Task CenterOnMyLocationAsync(CancellationToken cancellationToken = default)
    {
        if (usingCustomPosition)
        {
            return;
        }

        try
        {
            var location = await Geolocation.Default.GetLastKnownLocationAsync()
                           ?? await Geolocation.Default.GetLocationAsync(
                               new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(10)), cancellationToken);
            if (location is not null && !disposed)
            {
                presenter?.CenterOn(location.Latitude, location.Longitude, true);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
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
