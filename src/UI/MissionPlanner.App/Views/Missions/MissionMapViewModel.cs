using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MissionPlanner.App.Views.Missions.Workflows;
using MissionPlanner.Core.ConfigTuning.Planner;
using MissionPlanner.Core.DomainEvents;
using MissionPlanner.Core.Missions;
using MissionPlanner.Core.Missions.Models;
using MissionPlanner.Core.Missions.Planning;
using MissionPlanner.Core.Replay;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Library.DateTime.Domain;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.Library.Factory.Domain.Abstractions;
using MissionPlanner.Maps.Terrain;

namespace MissionPlanner.App.Views.Missions;

/// <summary>
/// Shared view model for the mission map editor. It tracks the vehicle, owns the mission plan being
/// edited, and exposes the commands behind the map's right-click context menu. It is registered as a
/// Keyed singleton so the FlightData map and the FlightPlanner screen does not edit the same mission.
/// </summary>
public partial class MissionMapViewModel : DialogViewModelBase, IMissionEditorHost
{
    private readonly IActiveVehicleContext activeVehicle;
    private readonly IDomainEventHub domainEventHub;
    private readonly IMissionRowEditor rowEditor;
    private readonly IMissionFileWorkflow missionFiles;
    private readonly IDateTimeProvider dateTimeProvider;
    private readonly IMissionMapInteractionCoordinator interaction;
    private readonly IMissionEditingWorkflow editing;
    private readonly IVehiclePlanWorkflow vehiclePlan;
    private readonly IPolygonPlanningWorkflow polygonPlanning;
    private readonly IGeneratedMissionWorkflow generatedMissions;
    private readonly IMapToolsWorkflow mapTools;
    private readonly IMissionElevationProfileService elevationProfileService;
    private readonly IReplaySessionManager replaySession;
    private readonly IPlannerSettingsService settingsService;

    private IDisposable? stateSubscription;
    private bool disposed;
    private bool isActive;

    /// <summary>
    /// Initializes a new instance of the <see cref="MissionMapViewModel"/> class.
    /// </summary>
    public MissionMapViewModel(IServiceFactory factory, ILogger logger)
    {
        domainEventHub = factory.Create<IDomainEventHub>();
        dateTimeProvider = factory.Create<IDateTimeProvider>();
        activeVehicle = factory.Create<IActiveVehicleContext>();
        rowEditor = AttachWorkflow(factory.Create<IMissionRowEditor>());
        rowEditor.SummaryChanged += summary => MissionSummary = summary;
        rowEditor.DirtyStateChanged += OnRowDirtyStateChanged;
        missionFiles = AttachWorkflow(factory.Create<IMissionFileWorkflow>());
        interaction = factory.Create<IMissionMapInteractionCoordinator>();
        editing = AttachWorkflow(factory.Create<IMissionEditingWorkflow>());
        vehiclePlan = AttachWorkflow(factory.Create<IVehiclePlanWorkflow>());
        polygonPlanning = AttachWorkflow(factory.Create<IPolygonPlanningWorkflow>());
        generatedMissions = AttachWorkflow(factory.Create<IGeneratedMissionWorkflow>());
        mapTools = AttachWorkflow(factory.Create<IMapToolsWorkflow>());
        elevationProfileService = factory.Create<IMissionElevationProfileService>();
        settingsService = factory.Create<IPlannerSettingsService>();
        replaySession = factory.Create<IReplaySessionManager>();
    }

    private TWorkflow AttachWorkflow<TWorkflow>(TWorkflow workflow) where TWorkflow : IMissionWorkflow
    {
        workflow.Attach(this);
        return workflow;
    }

    /// <summary>
    /// Activates the Flight Data page and its selected tab.
    /// </summary>
    public override async Task ActivateAsync()
    {
        if (disposed)
        {
            return;
        }
        if (isActive)
        {
            return;
        }
        SelectedSourceId = settingsService.Current.Map.SelectedSourceId;
        interaction.PendingRallyAltitude = DefaultAltitude();
        interaction.Changed += OnInteractionChanged;
        interaction.PlanningStateChanged += OnPlanningStateChanged;
        replaySession.Changed += OnReplaySessionChanged;

        await interaction.ActivateAsync();
        MapSnapshot = MissionMapProjection.Create(Mission, HomePosition);
        SelectedMapStyle = "GEO";

        activeVehicle.Changed += OnActiveVehicleChanged;
        stateSubscription = domainEventHub.SubscribeDomainEventAsync<VehicleStateUpdated>(OnVehicleStateUpdated);
        UpdateVehicleStatus(activeVehicle.Current);
        isActive = true;
    }


    /// <summary>
    /// Deactivates the Flight Data page
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
        activeVehicle.Changed -= OnActiveVehicleChanged;
        interaction.Changed -= OnInteractionChanged;
        interaction.PlanningStateChanged -= OnPlanningStateChanged;
        interaction.Deactivate();
        replaySession.Changed -= OnReplaySessionChanged;
        stateSubscription?.Dispose();
        stateSubscription = null;
    }

    /// <inheritdoc />
    public override Task DeactivateAsync()
    {
        Deactivate();
        return base.DeactivateAsync();
    }

    /// <inheritdoc />
    public override void Dispose()
    {
        if (disposed)
        {
            return;
        }
        Deactivate();
        disposed = true;
        interaction.Cancel();
        rowEditor.Dispose();
        base.Dispose();
    }

    /// <summary>
    /// Gets the coordinate display styles offered by the map status bar.
    /// </summary>
    public IReadOnlyList<string> MapStyles { get; } = ["GEO", "UTM", "MGRS"];

    /// <summary>
    /// Gets or sets the selected coordinate display style.
    /// </summary>
    [ObservableProperty]
    public partial string SelectedMapStyle
    {
        get; set;
    }

    /// <summary>Gets the pointer coordinate formatted in the selected coordinate style.</summary>
    [ObservableProperty]
    public partial string PointerCoordinateText { get; private set; } = MapPointerPresentation.PositionUnavailable;

    /// <summary>
    /// Gets the freshness of the latest map-position observation.
    /// </summary>
    [ObservableProperty]
    public partial string MapFreshness { get; private set; } = "Map: no position";

    [ObservableProperty]
    public partial double VehicleLatitude
    {
        get; set;
    }

    [ObservableProperty]
    public partial double VehicleLongitude
    {
        get; set;
    }

    /// <summary>Gets the latitude currently under the map pointer.</summary>
    [ObservableProperty]
    public partial double? PointerLatitude
    {
        get; private set;
    }

    /// <summary>Gets the longitude currently under the map pointer.</summary>
    [ObservableProperty]
    public partial double? PointerLongitude
    {
        get; private set;
    }


    /// <summary>Gets the altitude currently under the map pointer.</summary>
    [ObservableProperty]
    public partial double? PointerAltitude
    {
        get; private set;
    }

    /// <summary>Gets the typed state of the current pointer terrain lookup.</summary>
    [ObservableProperty]
    public partial TerrainElevationStatus PointerAltitudeStatus { get; private set; } = TerrainElevationStatus.Idle;

    /// <summary>Gets the user-facing pointer terrain status.</summary>
    [ObservableProperty]
    public partial string PointerAltitudeStatusText { get; private set; } = string.Empty;

    /// <summary>Gets whether pointer terrain status should be displayed.</summary>
    [ObservableProperty]
    public partial bool HasPointerAltitudeStatus
    {
        get; private set;
    }

    /// <summary>Gets the compact or expanded attribution displayed over the map.</summary>
    [ObservableProperty]
    public partial string AttributionText { get; private set; } = string.Empty;

    /// <summary>Gets whether the map attribution overlay has content.</summary>
    [ObservableProperty]
    public partial bool IsAttributionVisible
    {
        get; private set;
    }

    /// <summary>Updates the shared attribution overlay presentation.</summary>
    public void SetAttribution(string text)
    {
        AttributionText = text;
        IsAttributionVisible = !string.IsNullOrWhiteSpace(text);
    }

    [ObservableProperty]
    public partial double VehicleHeading
    {
        get; set;
    }

    /// <summary>When true the map keeps centering on the vehicle as telemetry arrives.</summary>
    [ObservableProperty]
    public partial bool FollowVehicle { get; set; } = true;

    /// <summary>Default altitude (meters, relative to home) applied to newly created mission items.</summary>
    [ObservableProperty]
    public partial double DefaultAltitudeMeters { get; set; } = 100;

    /// <summary>Planned home/launch position, set via "Set Home Here".</summary>
    [ObservableProperty]
    public partial GeoPosition? HomePosition
    {
        get; set;
    }

    /// <summary>Short feedback message for the last menu action.</summary>
    [ObservableProperty]
    public partial bool HasAltitudeMessage
    {
        get; set;
    }

    /// <summary>The stable catalog, pack, or custom source identifier rendered by map views.</summary>
    [ObservableProperty]
    public partial string SelectedSourceId { get; set; } = "osm-standard";

    /// <summary>When true, a primary map click appends a waypoint at the clicked position.</summary>
    [ObservableProperty]
    public partial bool AddWaypointOnMapClick
    {
        get; set;
    }

    /// <summary>Waypoint acceptance radius in meters (editor setting, v1.38 "WP Radius").</summary>
    [ObservableProperty]
    public partial double WaypointRadiusMeters { get; set; } = 30;

    /// <summary>Loiter radius in meters (editor setting, v1.38 "Loiter Radius").</summary>
    [ObservableProperty]
    public partial double LoiterRadiusMeters { get; set; } = 45;

    /// <summary>Altitude warning threshold in meters (editor setting, v1.38 "Alt Warn").</summary>
    [ObservableProperty]
    public partial double AltWarnMeters
    {
        get; set;
    }

    /// <summary>Summary line for the mission (item count, total distance).</summary>
    [ObservableProperty]
    public partial string MissionSummary { get; set; } = "0 items";

    /// <summary>Gets the stable built-in source identifiers offered by the compact map selector.</summary>
    public IReadOnlyList<string> AvailableSourceIds
    {
        get;
    } =
        ["osm-standard", "esri-world-topo", "esri-world-physical", "esri-world-shaded-relief", "esri-world-dark-gray", "no-map"];


    partial void OnPointerAltitudeChanged(double? oldValue, double? newValue)
    {
        HasAltitudeMessage = newValue is not null;
    }

    partial void OnSelectedMapStyleChanged(string value)
    {
        UpdatePointerCoordinateText();
    }

    /// <summary>The command names offered by the editor's Command select.</summary>
    public IReadOnlyList<string> CommandOptions { get; } = MissionCommandCatalog.CommandNames;

    /// <summary>The frame names offered by the editor's Frame select.</summary>
    public IReadOnlyList<string> FrameOptions { get; } = MissionCommandCatalog.FrameNames;

    /// <summary>
    /// Display rows for the mission items, kept in sync with <see cref="Mission"/>.
    /// </summary>
    public ObservableCollection<MissionItemRow> MissionItems => rowEditor.Rows;

    /// <summary>
    /// Display rows for the mission items that have been edited but not yet applied.
    /// </summary>
    public ObservableCollection<MissionItemRow> DirtyMissionItems => rowEditor.DirtyRows;

    /// <summary>
    /// The map position the context menu actions operate on (where the user right-clicked/tapped).
    /// Updated by the view before the menu opens.
    /// </summary>
    public GeoPosition? ContextPosition => MapContext?.Position;

    /// <summary>Gets whether the current mission contains at least one item.</summary>
    public bool HasMissionItems => Mission.Items.Count > 0;

    /// <summary>Gets whether a polygon-dependent command has valid local input.</summary>
    public bool HasPlanningPolygon => interaction.Polygons.Snapshot.Polygon is not null;

    /// <summary>Gets whether a route-dependent command has at least two geographic mission items.</summary>
    public bool HasMissionRoute => Mission.Items.Select(MissionItemGeometry.PositionOf).Count(position => position is not null) >= 2;

    /// <summary>Gets whether an imported overlay can be cleared.</summary>
    public bool HasImportedOverlay => polygonPlanning.ImportedOverlays.Count > 0;

    /// <summary>Gets whether outbound fence and rally commands are safe to offer.</summary>
    public bool CanUseVehicleCommands => activeVehicle.Current.IsOnline && !replaySession.Snapshot.IsTransmissionProhibited;

    /// <summary>
    /// Gets the explicit context snapshot used by location-sensitive commands. Pointer hover does
    /// not replace this value; mouse context clicks and ordinary touch taps do.
    /// </summary>
    public MissionMapContext? MapContext
    {
        get; private set;
    }

    /// <summary>Gets the UI-neutral presentation state consumed by mission map views.</summary>
    [ObservableProperty]
    public partial MissionMapSnapshot MapSnapshot
    {
        get; private set;
    }

    /// <summary>Gets the renderer-independent planning overlay state.</summary>
    [ObservableProperty]
    public partial MissionPlanningOverlaySnapshot PlanningOverlaySnapshot { get; private set; } = MissionPlanningOverlaySnapshot.Empty;

    /// <summary>Gets the current generated elevation profile.</summary>
    [ObservableProperty]
    public partial MissionElevationProfile? ElevationProfile
    {
        get; private set;
    }

    /// <summary>Gets whether the elevation graph overlay is visible.</summary>
    [ObservableProperty]
    public partial bool IsElevationProfileVisible
    {
        get; private set;
    }

    /// <summary>Gets the current planning interaction instruction.</summary>
    [ObservableProperty]
    public partial string PlanningInteractionPrompt { get; private set; } = string.Empty;

    /// <summary>The mission plan being edited.</summary>
    public Mission Mission { get; private set; } = new(MissionId.New(), "New Mission");

    /// <summary>Raised whenever the mission items change so the views can redraw pins and the route.</summary>
    public event Action<MissionEventArgs>? MissionChanged;

    /// <summary>Raised when the map should pan/zoom to show the whole mission (after load or vehicle read).</summary>
    public event Action? FitToMissionRequested;

    /// <summary>Raised when the session-only map rotation should change.</summary>
    public event Action<double>? MapRotationRequested;

    /// <summary>Raised when the map should center on a converted coordinate.</summary>
    public event Action<GeoPosition>? MapCenterRequested;

    /// <summary>Records the map position the next context-menu action should apply to.</summary>
    public void SetContextPosition(double latitude, double longitude)
    {
        MapContext = new MissionMapContext(new GeoPosition(latitude, longitude), MissionMapContextSource.ContextClick, dateTimeProvider.UtcNow);
    }

    /// <summary>Updates the bindable geographic coordinate currently under the map pointer.</summary>
    /// <param name="latitude">Latitude in degrees.</param>
    /// <param name="longitude">Longitude in degrees.</param>
    /// <param name="altitudeMeters">Terrain altitude in metres, when supplied by an elevation service.</param>
    public void SetPointerPosition(double latitude, double longitude, double? altitudeMeters)
    {
        PointerLatitude = latitude;
        PointerLongitude = longitude;
        UpdatePointerCoordinateText();
        PointerAltitude = altitudeMeters;
    }

    private void UpdatePointerCoordinateText()
    {
        PointerCoordinateText = MapPointerPresentation.CoordinateText(SelectedMapStyle, PointerLatitude, PointerLongitude);
    }

    /// <summary>Updates the typed terrain status while a pointer lookup is in progress.</summary>
    public void SetPointerElevationStatus(TerrainElevationStatus status)
    {
        PointerAltitude = null;
        ApplyPointerElevationStatus(status, null);
    }

    /// <summary>Applies a completed typed terrain result to the pointer presentation.</summary>
    public void SetPointerElevation(TerrainElevationResult result)
    {
        PointerAltitude = result.ElevationMeters;
        ApplyPointerElevationStatus(result.Status, result.Message);
    }

    private void ApplyPointerElevationStatus(TerrainElevationStatus status, string? message)
    {
        PointerAltitudeStatus = status;
        PointerAltitudeStatusText = MapPointerPresentation.TerrainStatusText(status, message);
        HasPointerAltitudeStatus = status != TerrainElevationStatus.Idle;
    }

    private void UpdateVehicleStatus(ActiveVehicleSnapshot snapshot)
    {
        MapFreshness = snapshot.State?.Position.ObservedAt is { } observedAt
            ? $"Map: {FormatAge(observedAt)}"
            : "Map: no position";
    }

    private void OnActiveVehicleChanged(ActiveVehicleChangedEventArgs e)
    {
        Dispatcher.Dispatch(() =>
        {
            UpdateVehicleStatus(e.Current);
            OnPropertyChanged(nameof(CanUseVehicleCommands));
        });
    }

    private void OnReplaySessionChanged(ReplaySessionChangedEventArgs args)
    {
        Dispatcher.Dispatch(() => OnPropertyChanged(nameof(CanUseVehicleCommands)));
    }

    private Task OnVehicleStateUpdated(VehicleStateUpdated evt, CancellationToken cancellationToken)
    {
        if (evt.VehicleId == activeVehicle.VehicleId)
        {
            Dispatcher.Dispatch(() =>
            {
                if (evt.VehicleId == activeVehicle.VehicleId)
                {
                    UpdateVehicleStatus(new ActiveVehicleSnapshot(evt.VehicleId, evt.VehicleState));
                }
            });
        }

        return Task.CompletedTask;
    }

    private string FormatAge(DateTimeOffset observedAt)
    {
        var age = dateTimeProvider.UtcNow - observedAt;
        return age <= TimeSpan.FromSeconds(2)
            ? "live"
            : age < TimeSpan.FromMinutes(1)
                ? $"{Math.Max(0, (int)age.TotalSeconds)}s old"
                : $"{Math.Max(0, (int)age.TotalMinutes)}m old";
    }

    /// <summary>Handles a primary map click according to the active map editing mode.</summary>
    public void HandleMapClick(double latitude, double longitude)
    {
        var position = new GeoPosition(latitude, longitude);
        MapContext = new MissionMapContext(position, MissionMapContextSource.Tap, dateTimeProvider.UtcNow);

        var outcome = interaction.HandleClick(position);
        if (outcome.Consumed)
        {
            if (outcome.Message is not null)
            {
                ShowStatus(outcome.Message);
            }

            return;
        }

        if (AddWaypointOnMapClick)
        {
            AddWaypoint(position, "Waypoint added from map click.");
        }
    }

    /// <summary>Routes map pointer movement to the active planning interaction.</summary>
    public void HandleMapPointerMove(double latitude, double longitude)
    {
        interaction.MovePointer(new GeoPosition(latitude, longitude));
    }

    /// <summary>Starts a renderer-independent planning interaction.</summary>
    public void BeginInteraction(MissionMapInteractionMode mode, string prompt)
    {
        interaction.Begin(mode, prompt);
    }

    /// <summary>Cancels the current planning interaction.</summary>
    public void CancelInteraction()
    {
        interaction.Cancel();
    }

    private void OnInteractionChanged(object? sender, EventArgs args)
    {
        Dispatcher.Dispatch(() =>
        {
            UpdatePlanningOverlay();
            PlanningInteractionPrompt = interaction.State.Prompt;
        });
    }

    private void OnPlanningStateChanged(object? sender, EventArgs args)
    {
        Dispatcher.Dispatch(() =>
        {
            UpdatePlanningOverlay();
            OnPropertyChanged(nameof(HasPlanningPolygon));
        });
    }

    private void UpdatePlanningOverlay()
    {
        PlanningOverlaySnapshot = interaction.ComposeOverlay(polygonPlanning.ImportedOverlays, generatedMissions.SurveyPreview);
        OnPropertyChanged(nameof(HasImportedOverlay));
    }

    /// <inheritdoc />
    public void ReplaceMission(Mission mission, string message)
    {
        Mission = mission;
        OnMissionChanged(message);
        FitToMissionRequested?.Invoke();
    }

    /// <summary>
    /// Clears the mission being edited and resets it to a new empty mission.   
    /// </summary>
    public void ClearMissionData()
    {
        HomePosition = null;
        Mission = new Mission(MissionId.New(), "New Mission");
        OnMissionChanged("Mission cleared.");
    }


    [RelayCommand]
    private void InsertWaypoint() => editing.InsertWaypoint();

    [RelayCommand]
    private void InsertSplineWaypoint() => editing.InsertSplineWaypoint();

    [RelayCommand]
    private Task JumpToStartAsync(CancellationToken cancellationToken) => editing.JumpToStartAsync(cancellationToken);

    [RelayCommand]
    private Task JumpToWaypointAsync(CancellationToken cancellationToken) => editing.JumpToWaypointAsync(cancellationToken);

    [RelayCommand]
    private void SetRoiHere() => editing.SetRoiHere();

    [RelayCommand]
    private void InsertWaypointAtVehicle() => editing.InsertWaypointAtVehicle();

    [RelayCommand]
    private void DeleteWaypoint() => editing.DeleteWaypoint();

    [RelayCommand]
    private void RemoveItem(MissionItemRow row) => editing.RemoveItem(row);

    [RelayCommand]
    private void MoveItemUp(MissionItemRow row) => editing.MoveItemUp(row);

    [RelayCommand]
    private void MoveItemDown(MissionItemRow row) => editing.MoveItemDown(row);

    [RelayCommand]
    private void LoiterForever() => editing.LoiterForever();

    [RelayCommand]
    private Task LoiterTimeAsync(CancellationToken cancellationToken) => editing.LoiterTimeAsync(cancellationToken);

    [RelayCommand]
    private Task LoiterCirclesAsync() => editing.LoiterCirclesAsync();

    [RelayCommand]
    private void AddReturnToLaunch() => editing.AddReturnToLaunch();

    [RelayCommand]
    private void AddLand() => editing.AddLand();

    [RelayCommand]
    private Task AddTakeoffAsync() => editing.AddTakeoffAsync();

    [RelayCommand]
    private void ReverseWaypoints() => editing.ReverseWaypoints();

    [RelayCommand]
    private Task ModifyAltitudeAsync() => editing.ModifyAltitudeAsync();

    [RelayCommand]
    private void DrawPolygon() => BeginInteraction(MissionMapInteractionMode.DrawPolygon, "Click at least three polygon vertices, then choose Finish Polygon.");

    [RelayCommand]
    private void FinishPolygon() => polygonPlanning.FinishPolygon();

    [RelayCommand]
    private void CancelPolygon() => polygonPlanning.CancelPolygon();

    [RelayCommand]
    private void ClearPolygon() => polygonPlanning.ClearPolygon();

    [RelayCommand]
    private void PolygonFromWaypoints() => polygonPlanning.PolygonFromWaypoints();

    [RelayCommand]
    private Task OffsetPolygonAsync(CancellationToken cancellationToken) => polygonPlanning.OffsetPolygonAsync(cancellationToken);

    [RelayCommand]
    private void ShowPolygonArea() => polygonPlanning.ShowPolygonArea();

    [RelayCommand]
    private Task SavePolygonAsync(CancellationToken cancellationToken) => polygonPlanning.SavePolygonAsync(cancellationToken);

    [RelayCommand]
    private Task LoadPolygonAsync(CancellationToken cancellationToken) => polygonPlanning.LoadPolygonAsync(cancellationToken);

    [RelayCommand]
    private Task KmlOverlayAsync(CancellationToken cancellationToken) => polygonPlanning.KmlOverlayAsync(cancellationToken);

    [RelayCommand]
    private void ClearImportedOverlay() => polygonPlanning.ClearImportedOverlay();

    [RelayCommand]
    private Task LoadKmlFileAsync(CancellationToken cancellationToken) => polygonPlanning.LoadKmlFileAsync(cancellationToken);

    [RelayCommand]
    private Task LoadShapefileAsync(CancellationToken cancellationToken) => polygonPlanning.LoadShapefileAsync(cancellationToken);

    [RelayCommand]
    private Task PolygonFromShapefileAsync(CancellationToken cancellationToken) => polygonPlanning.PolygonFromShapefileAsync(cancellationToken);

    [RelayCommand]
    private Task DownloadFenceAsync(CancellationToken cancellationToken) => vehiclePlan.DownloadFenceAsync(cancellationToken);

    [RelayCommand]
    private Task UploadFenceAsync(CancellationToken cancellationToken) => vehiclePlan.UploadFenceAsync(cancellationToken);

    [RelayCommand]
    private Task SaveFenceAsync(CancellationToken cancellationToken) => vehiclePlan.SaveFenceAsync(cancellationToken);

    [RelayCommand]
    private Task LoadFenceAsync(CancellationToken cancellationToken) => vehiclePlan.LoadFenceAsync(cancellationToken);

    [RelayCommand]
    private Task ClearFenceAsync(CancellationToken cancellationToken) => vehiclePlan.ClearFenceAsync(cancellationToken);

    [RelayCommand]
    private Task DownloadRallyAsync(CancellationToken cancellationToken) => vehiclePlan.DownloadRallyAsync(cancellationToken);

    [RelayCommand]
    private Task UploadRallyAsync(CancellationToken cancellationToken) => vehiclePlan.UploadRallyAsync(cancellationToken);

    [RelayCommand]
    private Task ClearRallyAsync(CancellationToken cancellationToken) => vehiclePlan.ClearRallyAsync(cancellationToken);

    [RelayCommand]
    private Task SaveRallyAsync(CancellationToken cancellationToken) => vehiclePlan.SaveRallyAsync(cancellationToken);

    [RelayCommand]
    private Task LoadRallyAsync(CancellationToken cancellationToken) => vehiclePlan.LoadRallyAsync(cancellationToken);

    [RelayCommand]
    private Task SetRallyPointAsync(CancellationToken cancellationToken) => vehiclePlan.SetRallyPointAsync(cancellationToken);

    [RelayCommand]
    private void SetFenceReturnLocation() => BeginInteraction(MissionMapInteractionMode.SetFenceReturnLocation, "Click the map to set the local fence return location.");

    [RelayCommand]
    private Task CreateWaypointCircleAsync(CancellationToken cancellationToken) => generatedMissions.CreateWaypointCircleAsync(cancellationToken);

    [RelayCommand]
    private Task CreateSplineCircleAsync(CancellationToken cancellationToken) => generatedMissions.CreateSplineCircleAsync(cancellationToken);

    [RelayCommand]
    private Task AutoWaypointTextAsync(CancellationToken cancellationToken) => generatedMissions.AutoWaypointTextAsync(cancellationToken);

    [RelayCommand]
    private Task CreateCircleSurveyAsync(CancellationToken cancellationToken) => generatedMissions.CreateCircleSurveyAsync(cancellationToken);

    [RelayCommand]
    private Task CreateGridSurveyAsync(CancellationToken cancellationToken) => generatedMissions.CreateGridSurveyAsync(cancellationToken);

    [RelayCommand]
    private void AutoWaypointArea() => polygonPlanning.ShowPolygonArea();

    [RelayCommand]
    private void MeasureDistance() => BeginInteraction(MissionMapInteractionMode.MeasureDistance, "Click two map points to measure geodesic distance and initial bearing.");

    [RelayCommand]
    private Task PrefetchVisibleAsync(CancellationToken cancellationToken) => mapTools.PrefetchVisibleAsync(cancellationToken);

    [RelayCommand]
    private Task PrefetchWaypointPathAsync(CancellationToken cancellationToken) => mapTools.PrefetchWaypointPathAsync(cancellationToken);

    [RelayCommand]
    private Task AddPoiAsync(CancellationToken cancellationToken) => mapTools.AddPoiAsync(cancellationToken);

    [RelayCommand]
    private Task EditPoiAsync(CancellationToken cancellationToken) => mapTools.EditPoiAsync(cancellationToken);

    [RelayCommand]
    private Task DeletePoiAsync(CancellationToken cancellationToken) => mapTools.DeletePoiAsync(cancellationToken);

    [RelayCommand]
    private Task SetTrackerHomeAsync(CancellationToken cancellationToken) => mapTools.SetTrackerHomeAsync(cancellationToken);

    [RelayCommand]
    private Task EnterUtmCoordinateAsync(CancellationToken cancellationToken) => mapTools.EnterUtmCoordinateAsync(cancellationToken);

    [RelayCommand]
    private Task RotateMapAsync(CancellationToken cancellationToken) => mapTools.RotateMapAsync(cancellationToken);

    [RelayCommand]
    private async Task ShowElevationGraphAsync(CancellationToken cancellationToken)
    {
        ShowStatus("Sampling mission terrain profile…");
        ElevationProfile = await elevationProfileService.GenerateAsync(new MissionElevationProfileRequest(Mission, HomePosition, null), cancellationToken);
        IsElevationProfileVisible = ElevationProfile.Samples.Count > 0;
        ShowStatus(IsElevationProfileVisible
            ? $"Elevation profile: {ElevationProfile.Samples.Count} samples, {ElevationProfile.UnavailableSamples} terrain gaps. Relative-to-home clearance requires home MSL altitude."
            : "Add at least two geographic mission items to create an elevation profile.");
    }

    [RelayCommand]
    private void CloseElevationGraph() => IsElevationProfileVisible = false;

    [RelayCommand]
    private void ClearMission() => ClearMissionData();

    [RelayCommand]
    private void SetHomeHere()
    {
        if (TargetPosition() is not { } position)
        {
            ShowStatus("No map position selected.");
            return;
        }

        HomePosition = position;
        ShowStatus($"Home set to {position.LatitudeDegrees:F6}, {position.LongitudeDegrees:F6}.");
    }

    [RelayCommand]
    private Task SaveWpFileAsync(CancellationToken cancellationToken) => missionFiles.SaveWpFileAsync(cancellationToken);

    [RelayCommand]
    private Task LoadWpFileAsync(CancellationToken cancellationToken) => missionFiles.LoadWpFileAsync(cancellationToken);

    [RelayCommand]
    private Task LoadAndAppendAsync(CancellationToken cancellationToken) => missionFiles.LoadAndAppendAsync(cancellationToken);

    [RelayCommand]
    private async Task CloseAsync(CancellationToken cancellationToken)
    {
        await domainEventHub.PublishDomainEventAsync(new EditorDisplayEvent("EditorClose"), cancellationToken);
    }
    /// <inheritdoc />
    public void AddWaypoint(GeoPosition position, string message)
    {
        if (!position.IsValid)
        {
            ShowStatus("Waypoint coordinates are invalid.");
            return;
        }

        Mission.Add(new WaypointMissionItem(MissionItemId.New(), 0, position, DefaultAltitude(), TimeSpan.Zero, WaypointRadiusMeters));
        OnMissionChanged(message);
    }

    /// <inheritdoc />
    public GeoPosition? TargetPosition()
    {
        return ContextPosition is { IsValid: true } context ? context : VehiclePosition;
    }

    /// <inheritdoc />
    public MissionAltitude DefaultAltitude()
    {
        return new MissionAltitude(DefaultAltitudeMeters, MissionAltitudeReference.Home);
    }

    /// <inheritdoc />
    public GeoPosition? VehiclePosition
    {
        get
        {
            var vehicle = new GeoPosition(VehicleLatitude, VehicleLongitude);
            return vehicle.IsValid && (VehicleLatitude != 0 || VehicleLongitude != 0) ? vehicle : null;
        }
    }

    IMissionMapInteractionCoordinator IMissionEditorHost.Interaction => interaction;

    void IMissionEditorHost.NotifyMissionChanged(string message) => OnMissionChanged(message);

    void IMissionEditorHost.SetMission(Mission mission, string message)
    {
        Mission = mission;
        OnMissionChanged(message);
    }

    void IMissionEditorHost.ClearPendingRowEdits() => rowEditor.ClearDirty();

    void IMissionEditorHost.RequestFitToMission() => FitToMissionRequested?.Invoke();

    void IMissionEditorHost.RefreshPlanningOverlay() => UpdatePlanningOverlay();

    void IMissionEditorHost.SetMapContext(GeoPosition position, MissionMapContextSource source)
    {
        MapContext = new MissionMapContext(position, source, dateTimeProvider.UtcNow);
    }

    void IMissionEditorHost.RequestMapCenter(GeoPosition position) => MapCenterRequested?.Invoke(position);

    void IMissionEditorHost.RequestMapRotation(double degrees) => MapRotationRequested?.Invoke(degrees);

    private void OnMissionChanged(string message)
    {
        rowEditor.Rebuild();
        UpdateMapSnapshot();
        MissionChanged?.Invoke(new MissionEventArgs(message));
        ShowStatus(message);
        OnPropertyChanged(nameof(HasMissionItems));
        OnPropertyChanged(nameof(HasMissionRoute));
    }

    private void UpdateMapSnapshot()
    {
        var snapshot = MissionMapProjection.Create(Mission, HomePosition);
        if (!MapSnapshot.ContentEquals(snapshot))
        {
            MapSnapshot = snapshot;
        }
    }

    partial void OnHomePositionChanged(GeoPosition? value)
    {
        UpdateMapSnapshot();
    }

    /// <summary>
    /// Applies the edited values of a row (params, lat/lon, altitude) back to the mission item.
    /// </summary>
    [RelayCommand(CanExecute = nameof(HasDirtyRows))]
    public void ApplyRowEdits() => rowEditor.ApplyAll();

    /// <summary>Returns whether any mission list row has pending edits.</summary>
    public bool HasDirtyRows() => rowEditor.HasDirtyRows();

    /// <summary>
    /// Cancels any edits made to the mission items and rebuilds the rows to their original state.
    /// </summary>
    [RelayCommand(CanExecute = nameof(HasDirtyRows))]
    public void CancelRowEdits() => rowEditor.Rebuild();

    private void OnRowDirtyStateChanged(object? sender, EventArgs args)
    {
        CancelRowEditsCommand.NotifyCanExecuteChanged();
        ApplyRowEditsCommand.NotifyCanExecuteChanged();
    }

    /// <inheritdoc />
    public void ShowStatus(string message)
    {
        Dispatcher.Dispatch(() => StatusMessage = message);
    }
}
