using Mapsui;
using Microsoft.Extensions.DependencyInjection;
using MissionPlanner.App.Services;
using MissionPlanner.App.Utilities;
using MissionPlanner.Library.Factory.Domain.Abstractions;

namespace MissionPlanner.App.Views.Missions;

/// <summary>
/// Shared mission-map editor control. Native map events remain at the view boundary while
/// <see cref="MissionMapPresenter"/> owns Mapsui rendering and navigation.
/// </summary>
public partial class MissionMapView : UserControlViewBase, IDisposable
{
    private MissionMapViewModel? viewModel;
    private readonly IDomainFactory domainFactory;
    private readonly IPlatformLocationService locationService;
    private MissionMapPresenter? presenter;
    private readonly SemaphoreSlim lifecycleGate = new(1, 1);
    private CancellationTokenSource? operationCancellation;
    private bool disposed;
    private bool isActive;

    /// <summary>Initializes a new instance of the <see cref="MissionMapView"/> class.</summary>
    public MissionMapView(IServiceProvider sp)
    {
        InitializeComponent();
        this.domainFactory = sp.GetRequiredService<IDomainFactory>();
        this.locationService = sp.GetRequiredService<IPlatformLocationService>();
    }

    /// <summary>Initializes a new instance of the <see cref="MissionMapView"/> class.</summary>
    public MissionMapView(IDomainFactory domainFactory, IPlatformLocationService locationService)
    {
        InitializeComponent();
        this.domainFactory = domainFactory;
        this.locationService = locationService;
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
                var latitude = this.viewModel.VehicleLatitude;
                var longitude = this.viewModel.VehicleLongitude;
                var hasVehiclePosition = double.IsFinite(latitude)
                    && double.IsFinite(longitude)
                    && latitude is >= -90 and <= 90
                    && longitude is >= -180 and <= 180
                    && (latitude != 0 || longitude != 0);
                if (hasVehiclePosition)
                {
                    presenter.CenterOn(latitude, longitude, true);
                }
                else
                {
                    await CenterOnMyLocationAsync(token);
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

    private async Task CenterOnMyLocationAsync(CancellationToken cancellationToken = default)
    {
        var location = await locationService.GetLocationAsync(cancellationToken);
        if (location is { } position && !disposed)
        {
            presenter?.CenterOn(position.LatitudeDegrees, position.LongitudeDegrees, true);
        }
    }

    private void OnZoomInClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs args)
    {
        presenter?.ZoomIn();
    }

    private void OnZoomOutClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs args)
    {
        presenter?.ZoomOut();
    }

    private void OnResetNorthClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs args)
    {
        presenter?.RotateTo(0);
    }

    private void OnZoomToVehicleClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs args)
    {
        presenter?.ZoomToVehicle();
    }

    private async void OnCenterOnMyLocationClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs args)
    {
        await CenterOnMyLocationAsync(operationCancellation?.Token ?? CancellationToken.None);
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
