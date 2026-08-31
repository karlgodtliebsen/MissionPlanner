using System.Diagnostics;
using Mapsui;
using MissionPlanner.AvaloniaUI.App.Utilities;
using MissionPlanner.Library.Factory.Domain.Abstractions;

namespace MissionPlanner.AvaloniaUI.App.Views.Missions;

/// <summary>
/// Shared mission-map editor control. Native map events remain at the view boundary while
/// <see cref="MissionMapPresenter"/> owns Mapsui rendering and navigation.
/// </summary>
public partial class MissionMapView : ViewBase, IDisposable
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
                if (this.viewModel is { VehicleLatitude: 0, VehicleLongitude: 0 })
                {
                    presenter.CenterOn(0, 0, true);
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
