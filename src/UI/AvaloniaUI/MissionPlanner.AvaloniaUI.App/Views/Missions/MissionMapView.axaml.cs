using Mapsui;
using MissionPlanner.AvaloniaUI.App.Utilities;
using MissionPlanner.Library.Factory.Domain.Abstractions;

namespace MissionPlanner.AvaloniaUI.App.Views.Missions;

/// <summary>
/// Shared mission-map editor control. Native map events remain at the view boundary while
/// <see cref="MissionMapPresenter"/> owns Mapsui rendering and navigation.
/// </summary>
public partial class MissionMapView : UserControlViewBase, IDisposable
{
    private MissionMapViewModel? viewModel;
    private readonly IDomainFactory domainFactory;
    private MissionMapPresenter? presenter;
    private readonly SemaphoreSlim lifecycleGate = new(1, 1);
    private CancellationTokenSource? operationCancellation;
    private bool disposed;
    private bool isActive;
    private readonly bool usingCustomPosition;

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
            MissionMap.MapTapped += OnMapTapped;
            MissionMap.MapPointerMoved += OnMapPointerMoved;
            this.viewModel.MapRotationRequested += OnMapRotationRequested;
            this.viewModel.MapCenterRequested += OnMapCenterRequested;
            DataContext = this.viewModel;
            presenter ??= domainFactory.Create<MissionMapPresenter, Mapsui.UI.Avalonia.MapControl, MissionMapViewModel>(MissionMap, this.viewModel);
            try
            {
                await presenter.ActivateAsync(token);
                token.ThrowIfCancellationRequested();
                // Establish an initial resolution regardless of whether telemetry populated the
                // vehicle position before this view became visible. Previously the default zoom
                // was applied only for (0, 0), so Flight Data opened at Mapsui's world extent
                // whenever it already had a valid vehicle position.
                var latitude = this.viewModel.VehicleLatitude;
                var longitude = this.viewModel.VehicleLongitude;
                var hasVehiclePosition = double.IsFinite(latitude)
                    && double.IsFinite(longitude)
                    && latitude is >= -90 and <= 90
                    && longitude is >= -180 and <= 180
                    && (latitude != 0 || longitude != 0);
                presenter.CenterOn(
                    hasVehiclePosition ? latitude : 0,
                    hasVehiclePosition ? longitude : 0,
                    true);
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
        MissionMap.MapTapped -= OnMapTapped;
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
        DataContext = null;
    }

    private void OnMapTapped(object? sender, MapEventArgs args)
    {
        var (longitude, latitude) = Mapsui.Projections.SphericalMercator.ToLonLat(args.WorldPosition.X, args.WorldPosition.Y);
        presenter?.HandleMapClick(latitude, longitude);
    }

    private void OnMapPointerMoved(object? sender, MapEventArgs args)
    {
        presenter?.UpdatePointerPosition(args.ScreenPosition.X, args.ScreenPosition.Y);
    }

    private void OnMapRotationRequested(double degrees)
    {
        presenter?.RotateTo(degrees);
    }

    private void OnMapCenterRequested(Core.Missions.Models.GeoPosition position)
    {
        presenter?.CenterOn(position.LatitudeDegrees, position.LongitudeDegrees, false);
    }

    private void OnZoomInClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs args)
    {
        presenter?.ZoomIn();
    }

    private void OnZoomOutClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs args)
    {
        presenter?.ZoomOut();
    }

    private void OnZoomToVehicleClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs args)
    {
        presenter?.ZoomToVehicle();
    }

    private void OnCenterOnMyLocationClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs args)
    {
        presenter?.ZoomToVehicle();
    }

    private void OnToggleFollowVehicleClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs args)
    {
        presenter?.ToggleFollowVehicle();
    }

    private void OnAttributionClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs args)
    {
        presenter?.ToggleAttribution();
    }
}
