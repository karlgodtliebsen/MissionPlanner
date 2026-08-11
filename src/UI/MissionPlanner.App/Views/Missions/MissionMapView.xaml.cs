using Mapsui.UI.Maui;
using Mapsui;
using MissionPlanner.App.Navigation;
using MissionPlanner.Core.ConfigTuning.Planner;
using MissionPlanner.Library;
using MissionPlanner.App.Maps;
using MissionPlanner.Maps.Sources;
using MissionPlanner.Maps.Attribution;
using MissionPlanner.Maps.Terrain;

namespace MissionPlanner.App.Views.Missions;

/// <summary>
/// Shared mission-map editor control. Native map events remain at the view boundary while
/// <see cref="MissionMapPresenter"/> owns Mapsui rendering and navigation.
/// </summary>
public partial class MissionMapView : ExtendedContentView<MissionMapViewModel>
{
    private readonly MissionMapPresenter presenter;
    private bool disposed;

    /// <summary>Initializes a new instance of the <see cref="MissionMapView"/> class.</summary>
    public MissionMapView(IPlannerSettingsService settingsService, IMapSourceResolver sourceResolver, IMapsuiBasemapFactory basemapFactory, IMapAttributionCoordinator attributionCoordinator, ITerrainElevationService terrainElevationService, MissionMapViewModel viewModel) : base(viewModel)
    {
        InitializeComponent();
        DomainException.ThrowIfNull(ViewModel);
        presenter = new MissionMapPresenter(MissionMap, ViewModel, settingsService, sourceResolver, basemapFactory, attributionCoordinator, terrainElevationService);

        MissionMap.MapClicked += OnMapClicked;
        MissionMap.MapPointerMoved += OnMapPointerMoved;
        ViewModel.MapRotationRequested += OnMapRotationRequested;
        ViewModel.MapCenterRequested += OnMapCenterRequested;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        Loaded += OnFirstLoaded;
    }

    private async void OnLoaded(object? sender, EventArgs args)
    {
        try
        {
            await presenter.ActivateAsync();
        }
        catch (OperationCanceledException)
        {
            // Unloading the view cancels in-flight source and attribution work.
        }
    }

    private void OnUnloaded(object? sender, EventArgs args) => presenter.Deactivate();

    private void OnMapClicked(object? sender, MapClickedEventArgs args)
    {
        presenter.HandleMapClick(args.Point.Latitude, args.Point.Longitude);
    }

    private void OnMapPointerMoved(object? sender, MapEventArgs args)
    {
        presenter.UpdatePointerPosition(args.ScreenPosition.X, args.ScreenPosition.Y);
    }

    private void OnMapRotationRequested(object? sender, double degrees) => presenter.RotateTo(degrees);
    private void OnMapCenterRequested(object? sender, MissionPlanner.Core.Missions.Models.GeoPosition position) => presenter.CenterOn(position.LatitudeDegrees, position.LongitudeDegrees, false);

    private async void OnFirstLoaded(object? sender, EventArgs args)
    {
        Loaded -= OnFirstLoaded;
        if (ViewModel is not { VehicleLatitude: 0, VehicleLongitude: 0 })
        {
            return;
        }

        await CenterOnMyLocationAsync();
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
                presenter.CenterOn(location.Latitude, location.Longitude, true);
            }
        }
        catch (Exception)
        {
            // Location permission missing or no provider available; retain the current viewport.
        }
    }

    private void OnZoomInClicked(object? sender, EventArgs args)
    {
        presenter.ZoomIn();
    }

    private void OnZoomOutClicked(object? sender, EventArgs args)
    {
        presenter.ZoomOut();
    }

    private void OnZoomToVehicleClicked(object? sender, EventArgs args)
    {
        presenter.ZoomToVehicle();
    }

    private async void OnCenterOnMyLocationClicked(object? sender, EventArgs args)
    {
        await CenterOnMyLocationAsync();
    }

    private void OnToggleFollowVehicleClicked(object? sender, EventArgs args)
    {
        presenter.ToggleFollowVehicle();
    }

    private void OnAttributionClicked(object? sender, EventArgs args)
    {
        presenter.ToggleAttribution();
    }

    /// <inheritdoc />
    public override void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        Loaded -= OnFirstLoaded;
        Loaded -= OnLoaded;
        Unloaded -= OnUnloaded;
        MissionMap.MapClicked -= OnMapClicked;
        MissionMap.MapPointerMoved -= OnMapPointerMoved;
        if (ViewModel is not null) { ViewModel.MapRotationRequested -= OnMapRotationRequested; ViewModel.MapCenterRequested -= OnMapCenterRequested; }
        presenter.Dispose();

        // The keyed mission editor is shared by the map and item-list views. Detach this view
        // without disposing that DI-owned singleton; the service provider owns its lifetime.
        BindingContext = null;
        ViewModel = null;
    }
}
