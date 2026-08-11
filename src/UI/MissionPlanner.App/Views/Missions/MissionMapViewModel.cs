using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ExCSS;
using Microsoft.Extensions.Logging;
using MissionPlanner.Core.ConfigTuning.Planner;
using MissionPlanner.Core.ConfigTuning.Fences;
using MissionPlanner.Core.DomainEvents;
using MissionPlanner.Core.Missions.Abstractions;
using MissionPlanner.Core.Missions.Files;
using MissionPlanner.Core.Missions.Models;
using MissionPlanner.Core.Missions.Planning;
using MissionPlanner.Core.Missions.Rally;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Library.DateTime.Domain;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.MavLink.Missions;
using MissionPlanner.Maps.Coordinates;
using MissionPlanner.Maps.Terrain;
using MissionPlanner.Maps.Prefetch;
using MissionPlanner.App.Presentation;

namespace MissionPlanner.App.Views.Missions;

/// <summary>
/// Shared view model for the mission map editor. It tracks the vehicle, owns the mission plan being
/// edited, and exposes the commands behind the map's right-click context menu. It is registered as a
/// Keyed singleton so the FlightData map and the FlightPlanner screen does not edit the same mission.
/// </summary>
public partial class MissionMapViewModel : ObservableObject, IDisposable
{
    private readonly IActiveVehicleContext activeVehicle;
    private readonly IMissionFileCodec fileCodec;
    private readonly IDomainEventHub domainEventHub;
    private readonly IDispatcher dispatcher;
    private readonly IMissionProtocolMapper protocolMapper;
    private readonly IFileSaver fileSaver;
    private readonly IDateTimeProvider dateTimeProvider;
    private readonly ILogger<MissionMapViewModel> logger;
    private readonly IMissionMapInteractionService interactionService;
    private readonly IAdvancedMissionItemService advancedMissionItems;
    private readonly IUserPromptService promptService;
    private readonly IUserConfirmationService confirmationService;
    private readonly IPlanningPolygonService polygonService;
    private readonly IFileOpenService fileOpenService;
    private readonly IFileSaveService fileSaveService;
    private readonly IUserChoiceService choiceService;
    private readonly IGeospatialImportService geospatialImportService;
    private readonly IFenceConfigurationService fenceService;
    private readonly IFencePlanFileCodec fenceFileCodec;
    private readonly IRallyConfigurationService rallyService;
    private readonly IRallyPlanFileCodec rallyFileCodec;
    private readonly IAutoWaypointGenerator autoWaypointGenerator;
    private readonly ISurveyMissionGenerator surveyMissionGenerator;
    private readonly IMapTilePrefetchService mapTilePrefetchService;
    private readonly IMissionElevationProfileService elevationProfileService;
    private readonly IPoiService poiService;
    private readonly ITrackerHomeService trackerHomeService;
    private readonly IGeodeticCoordinateConverter geodeticConverter;
    private IReadOnlyList<GeoPosition> generatedPreview = [];
    private MissionAltitude pendingRallyAltitude;
    private IReadOnlyList<ImportedPlanningOverlay> importedOverlays = [];
    private IDisposable? stateSubscription;
    private bool disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="MissionMapViewModel"/> class.
    /// </summary>
    public MissionMapViewModel(IActiveVehicleContext activeVehicle, IMissionProtocolMapper protocolMapper,
        IFileSaver fileSaver, IPlannerSettingsService settingsService,
        IMissionFileCodec fileCodec, IDomainEventHub domainEventHub, IDispatcher dispatcher,
        IDateTimeProvider dateTimeProvider, ILogger<MissionMapViewModel> logger,
        IMissionMapInteractionService interactionService, IAdvancedMissionItemService advancedMissionItems,
        IUserPromptService promptService, IUserConfirmationService confirmationService,
        IPlanningPolygonService polygonService, IFileOpenService fileOpenService, IFileSaveService fileSaveService,
        IUserChoiceService choiceService, IGeospatialImportService geospatialImportService,
        IFenceConfigurationService fenceService, IFencePlanFileCodec fenceFileCodec,
        IRallyConfigurationService rallyService, IRallyPlanFileCodec rallyFileCodec,
        IAutoWaypointGenerator autoWaypointGenerator, ISurveyMissionGenerator surveyMissionGenerator,
        IMapTilePrefetchService mapTilePrefetchService, IMissionElevationProfileService elevationProfileService,
        IPoiService poiService, ITrackerHomeService trackerHomeService, IGeodeticCoordinateConverter geodeticConverter)
    {
        this.activeVehicle = activeVehicle;
        this.fileCodec = fileCodec;
        this.domainEventHub = domainEventHub;
        this.dispatcher = dispatcher;
        this.protocolMapper = protocolMapper;
        this.fileSaver = fileSaver;
        this.dateTimeProvider = dateTimeProvider;
        this.logger = logger;
        this.interactionService = interactionService;
        this.advancedMissionItems = advancedMissionItems;
        this.promptService = promptService;
        this.confirmationService = confirmationService;
        this.polygonService = polygonService;
        this.fileOpenService = fileOpenService;
        this.fileSaveService = fileSaveService;
        this.choiceService = choiceService;
        this.geospatialImportService = geospatialImportService;
        this.fenceService = fenceService;
        this.fenceFileCodec = fenceFileCodec;
        this.rallyService = rallyService;
        this.rallyFileCodec = rallyFileCodec;
        this.autoWaypointGenerator = autoWaypointGenerator;
        this.surveyMissionGenerator = surveyMissionGenerator;
        this.mapTilePrefetchService = mapTilePrefetchService;
        this.elevationProfileService = elevationProfileService;
        this.poiService = poiService;
        this.trackerHomeService = trackerHomeService;
        this.geodeticConverter = geodeticConverter;
        pendingRallyAltitude = DefaultAltitude();
        polygonService.Changed += OnPolygonChanged;
        interactionService.Changed += OnInteractionChanged;
        fenceService.Changed += OnFenceChanged;
        rallyService.Changed += OnRallyChanged;
        poiService.Changed += OnPoiChanged;
        trackerHomeService.Changed += OnTrackerHomeChanged;
        _ = poiService.InitializeAsync();
        SelectedSourceId = settingsService.Current.Map.SelectedSourceId;
        MapSnapshot = MissionMapProjection.Create(Mission, HomePosition);
        SelectedMapStyle = "GEO";
        UpdateVehicleStatus(activeVehicle.Current);
        Activate();
    }

    /// <summary>
    /// Activates the Flight Data page and its selected tab.
    /// </summary>
    private void Activate()
    {
        activeVehicle.Changed += OnActiveVehicleChanged;
        stateSubscription = domainEventHub.SubscribeDomainEventAsync<VehicleStateUpdated>(OnVehicleStateUpdated);
        UpdateVehicleStatus(activeVehicle.Current);
    }

    /// <summary>
    /// Deactivates the Flight Data page
    /// </summary>
    private void Deactivate()
    {
        if (disposed)
        {
            return;
        }

        activeVehicle.Changed -= OnActiveVehicleChanged;
        interactionService.Cancel();
        stateSubscription?.Dispose();
        stateSubscription = null;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Deactivate();
        foreach (var row in MissionItems)
        {
            row.Dispose();
        }

        interactionService.Changed -= OnInteractionChanged;
        polygonService.Changed -= OnPolygonChanged;
        fenceService.Changed -= OnFenceChanged;
        rallyService.Changed -= OnRallyChanged;
        poiService.Changed -= OnPoiChanged;
        trackerHomeService.Changed -= OnTrackerHomeChanged;
        disposed = true;
    }

    /// <summary>
    /// Gets the coordinate display styles offered by the map status bar.
    /// </summary>
    public IReadOnlyList<string> MapStyles { get; } = ["GEO", "UTM", "MGRS"];

    /// <summary>
    /// Gets or sets the selected coordinate display style.
    /// </summary>
    [ObservableProperty]
    public partial string SelectedMapStyle { get; set; }

    /// <summary>Gets the pointer coordinate formatted in the selected coordinate style.</summary>
    [ObservableProperty]
    public partial string PointerCoordinateText { get; private set; } = "Position unavailable";

    /// <summary>
    /// Gets the active vehicle display name.
    /// </summary>
    [ObservableProperty]
    public partial string VehicleDisplayName { get; private set; } = "No vehicle";

    /// <summary>
    /// Gets the active vehicle connection status.
    /// </summary>
    [ObservableProperty]
    public partial string ConnectionStatus { get; private set; } = "Offline";

    /// <summary>
    /// Gets the freshness of the latest general telemetry observation.
    /// </summary>
    [ObservableProperty]
    public partial string TelemetryFreshness { get; private set; } = "Telemetry: unavailable";

    /// <summary>
    /// Gets the freshness of the latest map-position observation.
    /// </summary>
    [ObservableProperty]
    public partial string MapFreshness { get; private set; } = "Map: no position";

    [ObservableProperty] public partial double VehicleLatitude { get; set; }

    [ObservableProperty] public partial double VehicleLongitude { get; set; }

    /// <summary>Gets the latitude currently under the map pointer.</summary>
    [ObservableProperty]
    public partial double? PointerLatitude { get; private set; }

    /// <summary>Gets the longitude currently under the map pointer.</summary>
    [ObservableProperty]
    public partial double? PointerLongitude { get; private set; }


    /// <summary>Gets the altitude currently under the map pointer.</summary>
    [ObservableProperty]
    public partial double? PointerAltitude { get; private set; }

    /// <summary>Gets the typed state of the current pointer terrain lookup.</summary>
    [ObservableProperty]
    public partial TerrainElevationStatus PointerAltitudeStatus { get; private set; } = TerrainElevationStatus.Idle;

    /// <summary>Gets the user-facing pointer terrain status.</summary>
    [ObservableProperty]
    public partial string PointerAltitudeStatusText { get; private set; } = string.Empty;

    /// <summary>Gets whether pointer terrain status should be displayed.</summary>
    [ObservableProperty]
    public partial bool HasPointerAltitudeStatus { get; private set; }

    /// <summary>Gets the compact or expanded attribution displayed over the map.</summary>
    [ObservableProperty]
    public partial string AttributionText { get; private set; } = string.Empty;

    /// <summary>Gets whether the map attribution overlay has content.</summary>
    [ObservableProperty]
    public partial bool IsAttributionVisible { get; private set; }

    /// <summary>Updates the shared attribution overlay presentation.</summary>
    public void SetAttribution(string text)
    {
        AttributionText = text;
        IsAttributionVisible = !string.IsNullOrWhiteSpace(text);
    }


    [ObservableProperty] public partial double VehicleHeading { get; set; }
    // [ObservableProperty] public partial bool DirtyRows { get; set; }

    /// <summary>When true the map keeps centering on the vehicle as telemetry arrives.</summary>
    [ObservableProperty]
    public partial bool FollowVehicle { get; set; } = true;

    /// <summary>Default altitude (meters, relative to home) applied to newly created mission items.</summary>
    [ObservableProperty]
    public partial double DefaultAltitudeMeters { get; set; } = 100;

    /// <summary>Planned home/launch position, set via "Set Home Here".</summary>
    [ObservableProperty]
    public partial GeoPosition? HomePosition { get; set; }

    /// <summary>Short feedback message for the last menu action.</summary>
    [ObservableProperty]
    public partial string? StatusMessage { get; set; }


    /// <summary>Short feedback message for the last menu action.</summary>
    [ObservableProperty]
    public partial bool HasStatusMessage { get; set; }

    /// <summary>Short feedback message for the last menu action.</summary>
    [ObservableProperty]
    public partial bool HasAltitueMessage { get; set; }

    /// <summary>The stable catalog, pack, or custom source identifier rendered by map views.</summary>
    [ObservableProperty]
    public partial string SelectedSourceId { get; set; } = "osm-standard";

    /// <summary>When true, a primary map click appends a waypoint at the clicked position.</summary>
    [ObservableProperty]
    public partial bool AddWaypointOnMapClick { get; set; }

    /// <summary>Waypoint acceptance radius in meters (editor setting, v1.38 "WP Radius").</summary>
    [ObservableProperty]
    public partial double WaypointRadiusMeters { get; set; } = 30;

    /// <summary>Loiter radius in meters (editor setting, v1.38 "Loiter Radius").</summary>
    [ObservableProperty]
    public partial double LoiterRadiusMeters { get; set; } = 45;

    /// <summary>Altitude warning threshold in meters (editor setting, v1.38 "Alt Warn").</summary>
    [ObservableProperty]
    public partial double AltWarnMeters { get; set; }

    /// <summary>Summary line for the mission (item count, total distance).</summary>
    [ObservableProperty]
    public partial string MissionSummary { get; set; } = "0 items";

    /// <summary>Gets the stable built-in source identifiers offered by the compact map selector.</summary>
    public IReadOnlyList<string> AvailableSourceIds { get; } =
        ["osm-standard", "esri-world-topo", "esri-world-physical", "esri-world-shaded-relief", "esri-world-dark-gray", "no-map"];

    partial void OnStatusMessageChanged(string? value)
    {
        HasStatusMessage = value is not null;
    }

    partial void OnPointerAltitudeChanged(double? oldValue, double? newValue)
    {
        HasAltitueMessage = newValue is not null;
    }

    partial void OnSelectedMapStyleChanged(string value) => UpdatePointerCoordinateText();

    /// <summary>
    /// Commands selectable in the waypoint editor. Names follow v1.38's mavcmd.xml; the set is
    /// limited to the commands the mission domain supports.
    /// </summary>
    private static readonly (string Name, ushort Id)[] commandDefinitions =
    [
        ("WAYPOINT", 16),
        ("LOITER_UNLIM", 17),
        ("LOITER_TURNS", 18),
        ("LOITER_TIME", 19),
        ("RETURN_TO_LAUNCH", 20),
        ("LAND", 21),
        ("TAKEOFF", 22),
        ("SPLINE_WAYPOINT", 82),
        ("DO_JUMP", 177),
        ("DO_CHANGE_SPEED", 178),
        ("DO_SET_ROI_LOCATION", 195)
    ];

    /// <summary>Altitude frames selectable in the waypoint editor (v1.38 altmode naming).</summary>
    private static readonly (string Name, byte Id)[] frameDefinitions =
    [
        ("Absolute", 0),
        ("Mission", 2),
        ("Relative", 3),
        ("Terrain", 10)
    ];

    /// <summary>The command names offered by the editor's Command select.</summary>
    public IReadOnlyList<string> CommandOptions { get; } = commandDefinitions.Select(x => x.Name).ToArray();

    /// <summary>The frame names offered by the editor's Frame select.</summary>
    public IReadOnlyList<string> FrameOptions { get; } = frameDefinitions.Select(x => x.Name).ToArray();

    /// <summary>
    /// Display rows for the mission items, kept in sync with <see cref="Mission"/>.
    /// </summary>
    public ObservableCollection<MissionItemRow> MissionItems { get; } = [];

    /// <summary>
    /// Display rows for the mission items that have been edited but not yet applied.
    /// </summary>
    public ObservableCollection<MissionItemRow> DirtyMissionItems { get; } = [];

    /// <summary>
    /// The map position the context menu actions operate on (where the user right-clicked/tapped).
    /// Updated by the view before the menu opens.
    /// </summary>
    public GeoPosition? ContextPosition { get; private set; }

    /// <summary>Gets the UI-neutral presentation state consumed by mission map views.</summary>
    [ObservableProperty]
    public partial MissionMapSnapshot MapSnapshot { get; private set; }

    /// <summary>Gets the renderer-independent planning overlay state.</summary>
    [ObservableProperty]
    public partial MissionPlanningOverlaySnapshot PlanningOverlaySnapshot { get; private set; } = MissionPlanningOverlaySnapshot.Empty;
    /// <summary>Gets the current generated elevation profile.</summary>
    [ObservableProperty] public partial MissionElevationProfile? ElevationProfile { get; private set; }
    /// <summary>Gets whether the elevation graph overlay is visible.</summary>
    [ObservableProperty] public partial bool IsElevationProfileVisible { get; private set; }

    /// <summary>Gets the current planning interaction instruction.</summary>
    [ObservableProperty]
    public partial string PlanningInteractionPrompt { get; private set; } = string.Empty;

    /// <summary>The mission plan being edited.</summary>
    public Mission Mission { get; private set; } = new(MissionId.New(), "New Mission");

    /// <summary>Raised whenever the mission items change so the views can redraw pins and the route.</summary>
    public event EventHandler? MissionChanged;

    /// <summary>Raised when the map should pan/zoom to show the whole mission (after load or vehicle read).</summary>
    public event EventHandler? FitToMissionRequested;
    /// <summary>Raised when the session-only map rotation should change.</summary>
    public event EventHandler<double>? MapRotationRequested;
    /// <summary>Raised when the map should center on a converted coordinate.</summary>
    public event EventHandler<GeoPosition>? MapCenterRequested;

    /// <summary>Records the map position the next context-menu action should apply to.</summary>
    public void SetContextPosition(double latitude, double longitude)
    {
        ContextPosition = new GeoPosition(latitude, longitude);
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
        SetContextPosition(latitude, longitude);
    }

    private void UpdatePointerCoordinateText()
    {
        PointerCoordinateText = PointerLatitude is { } latitude && PointerLongitude is { } longitude
            ? MapCoordinateFormatter.Format(SelectedMapStyle, latitude, longitude)
            : "Position unavailable";
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
        PointerAltitudeStatusText = status switch
        {
            TerrainElevationStatus.Loading => "Terrain: loading",
            TerrainElevationStatus.Available => "Terrain: available",
            TerrainElevationStatus.OutsideCoverage => "Terrain: outside coverage",
            TerrainElevationStatus.NetworkUnavailable => "Terrain: network unavailable",
            TerrainElevationStatus.InvalidData => "Terrain: invalid data",
            _ => string.Empty
        };
        if (!string.IsNullOrWhiteSpace(message) && status is not TerrainElevationStatus.Available)
            PointerAltitudeStatusText += $" ({message})";
        HasPointerAltitudeStatus = status != TerrainElevationStatus.Idle;
    }

    private void UpdateVehicleStatus(ActiveVehicleSnapshot snapshot)
    {
        VehicleDisplayName = snapshot.DisplayName;
        ConnectionStatus = snapshot.State?.ConnectionState.ToString() ?? "Offline";
        TelemetryFreshness = snapshot.State is null
            ? "Telemetry: unavailable"
            : $"Telemetry: {FormatAge(snapshot.State.LastHeartbeatAt)}";
        MapFreshness = snapshot.State?.Position.ObservedAt is { } observedAt
            ? $"Map: {FormatAge(observedAt)}"
            : "Map: no position";
    }

    private void OnActiveVehicleChanged(object? sender, ActiveVehicleChangedEventArgs e)
    {
        dispatcher.Dispatch(() => UpdateVehicleStatus(e.Current));
    }

    private Task OnVehicleStateUpdated(VehicleStateUpdated evt, CancellationToken cancellationToken)
    {
        if (evt.VehicleId == activeVehicle.VehicleId)
        {
            dispatcher.Dispatch(() =>
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
        ContextPosition = position;

        var interactionMode = interactionService.State.Mode;
        if (interactionService.AcceptClick(position))
        {
            if (interactionMode == MissionMapInteractionMode.SetFenceReturnLocation && activeVehicle.VehicleId is { } vehicleId)
            {
                var snapshot = fenceService.GetSnapshot(vehicleId);
                fenceService.SetLocalPlan(vehicleId, snapshot.LocalPlan with { ReturnPoint = position });
                interactionService.Complete();
                ShowStatus("Fence return location updated locally; upload to apply it.");
            }
            else if (interactionMode == MissionMapInteractionMode.SetRallyPoint && activeVehicle.VehicleId is { } rallyVehicleId)
            {
                var snapshot = rallyService.GetSnapshot(rallyVehicleId);
                var point = new RallyPoint(RallyPointId.New(), position, pendingRallyAltitude);
                rallyService.SetLocalPlan(rallyVehicleId, new RallyPlan(snapshot.LocalPlan.Points.Append(point).ToArray()));
                interactionService.Complete();
                ShowStatus("Rally point added locally; upload to apply it.");
            }
            else if (interactionMode == MissionMapInteractionMode.MeasureDistance && interactionService.State.Positions.Count >= 2)
            {
                var first = interactionService.State.Positions[0]; var second = interactionService.State.Positions[1];
                var (distance, bearing) = CalculateDistanceAndBearing(first, second);
                interactionService.Complete();
                ShowStatus($"Distance {distance:F1} m • initial bearing {bearing:F1}°.");
            }
            return;
        }

        if (!AddWaypointOnMapClick)
        {
            return;
        }

        AddWaypoint(position, "Waypoint added from map click.");
    }

    /// <summary>Routes map pointer movement to the active planning interaction.</summary>
    public void HandleMapPointerMove(double latitude, double longitude) =>
        interactionService.MovePointer(new GeoPosition(latitude, longitude));

    /// <summary>Starts a renderer-independent planning interaction.</summary>
    public void BeginInteraction(MissionMapInteractionMode mode, string prompt) => interactionService.Enter(mode, prompt);

    /// <summary>Cancels the current planning interaction.</summary>
    public void CancelInteraction() => interactionService.Cancel();

    private void OnInteractionChanged(object? sender, EventArgs args)
    {
        dispatcher.Dispatch(() =>
        {
            UpdatePlanningOverlay();
            PlanningInteractionPrompt = interactionService.State.Prompt;
        });
    }

    private void OnPolygonChanged(object? sender, EventArgs args) => dispatcher.Dispatch(UpdatePlanningOverlay);
    private void OnFenceChanged(object? sender, EventArgs args) => dispatcher.Dispatch(UpdatePlanningOverlay);
    private void OnRallyChanged(object? sender, EventArgs args) => dispatcher.Dispatch(UpdatePlanningOverlay);
    private void OnPoiChanged(object? sender, EventArgs args) => dispatcher.Dispatch(UpdatePlanningOverlay);
    private void OnTrackerHomeChanged(object? sender, EventArgs args) => dispatcher.Dispatch(UpdatePlanningOverlay);

    private void UpdatePlanningOverlay()
    {
        var overlay = interactionService.Overlay;
        var vertices = interactionService.State.Mode == MissionMapInteractionMode.DrawPolygon
            ? overlay.DrawnPolygon
            : polygonService.Snapshot.Polygon?.Vertices ?? [];
        var fence = activeVehicle.VehicleId is { } vehicleId ? FenceOutline(fenceService.GetSnapshot(vehicleId).LocalPlan) : [];
        var rally = activeVehicle.VehicleId is { } rallyVehicleId
            ? rallyService.GetSnapshot(rallyVehicleId).LocalPlan.Points.Select(point => point.Position).ToArray() : [];
        var pois = poiService.Snapshot.Items.Select(item => item.Position).ToArray();
        PlanningOverlaySnapshot = overlay with { DrawnPolygon = vertices, FencePreview = fence, RallyPoints = rally,
            PoiItems = pois, ImportedOverlays = importedOverlays, SurveyPreview = generatedPreview,
            TrackerHome = trackerHomeService.Snapshot?.Position };
    }

    /// <summary>Replaces the mission being edited (e.g. after downloading from a vehicle).</summary>
    public void ReplaceMission(Mission mission, string message)
    {
        Mission = mission;
        OnMissionChanged(message);
        FitToMissionRequested?.Invoke(this, EventArgs.Empty);
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
    private void InsertWaypoint()
    {
        if (TargetPosition() is not { } position)
        {
            ShowStatus("No map position selected.");
            return;
        }

        AddWaypoint(position, $"Waypoint {Mission.Items.Count + 1} added.");
    }

    [RelayCommand]
    private void InsertSplineWaypoint()
    {
        if (TargetPosition() is not { } position)
        {
            ShowStatus("No map position selected.");
            return;
        }
        ApplyAdvancedResult(advancedMissionItems.AddSplineWaypoint(Mission, position, DefaultAltitude()), "Spline waypoint added.");
    }

    [RelayCommand]
    private async Task JumpToStartAsync(CancellationToken cancellationToken)
    {
        var repeat = await PromptRepeatCountAsync(cancellationToken);
        if (repeat is not null)
            ApplyAdvancedResult(advancedMissionItems.AddJumpToStart(Mission, repeat.Value), "DO_JUMP to mission start added.");
    }

    [RelayCommand]
    private async Task JumpToWaypointAsync(CancellationToken cancellationToken)
    {
        var targetText = await promptService.PromptAsync("Jump to waypoint", "Target mission row (1-based)", "1", cancellationToken);
        if (!ushort.TryParse(targetText, NumberStyles.Integer, CultureInfo.CurrentCulture, out var displayTarget) || displayTarget == 0)
        {
            if (targetText is not null)
                ShowStatus("Enter an existing mission row number.");
            return;
        }
        var repeat = await PromptRepeatCountAsync(cancellationToken);
        if (repeat is not null)
            ApplyAdvancedResult(advancedMissionItems.AddJump(Mission, (ushort)(displayTarget - 1), repeat.Value), $"DO_JUMP to row {displayTarget} added.");
    }

    [RelayCommand]
    private void SetRoiHere()
    {
        if (TargetPosition() is not { } position)
        {
            ShowStatus("No map position selected.");
            return;
        }
        ApplyAdvancedResult(advancedMissionItems.AddRoiLocation(Mission, position, DefaultAltitude()), "ROI location added.");
    }

    private async Task<int?> PromptRepeatCountAsync(CancellationToken cancellationToken)
    {
        var text = await promptService.PromptAsync("DO_JUMP", "Repeat count (use -1 for infinite)", "1", cancellationToken);
        if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.CurrentCulture, out var repeat) || repeat < -1)
        {
            if (text is not null)
                ShowStatus("Repeat count must be -1, zero, or positive.");
            return null;
        }
        if (repeat == -1 && !await confirmationService.ConfirmAsync("Infinite DO_JUMP", "This jump repeats indefinitely until the flight mode changes. Add it?", "Add", cancellationToken))
            return null;
        return repeat;
    }

    private void ApplyAdvancedResult(MissionMapCommandAvailability result, string successMessage)
    {
        if (!result.IsEnabled)
        {
            ShowStatus(result.Reason ?? "The mission item could not be added.");
            return;
        }
        OnMissionChanged(successMessage);
    }

    [RelayCommand]
    private void DrawPolygon() => BeginInteraction(MissionMapInteractionMode.DrawPolygon, "Click at least three polygon vertices, then choose Finish Polygon.");

    [RelayCommand]
    private void FinishPolygon()
    {
        if (interactionService.State.Mode != MissionMapInteractionMode.DrawPolygon)
        {
            ShowStatus("Polygon drawing is not active.");
            return;
        }
        var result = polygonService.Set("Planning polygon", interactionService.State.Positions);
        if (!result.Succeeded) { ShowStatus(result.Message); return; }
        interactionService.Complete();
        ShowStatus(result.Message);
    }

    [RelayCommand]
    private void CancelPolygon()
    {
        interactionService.Cancel();
        ShowStatus("Polygon drawing cancelled.");
    }

    [RelayCommand]
    private void ClearPolygon()
    {
        polygonService.Clear();
        interactionService.Cancel();
        ShowStatus("Planning polygon cleared.");
    }

    [RelayCommand]
    private void PolygonFromWaypoints()
    {
        var result = polygonService.FromMission(Mission);
        ShowStatus(result.Message);
    }

    [RelayCommand]
    private async Task OffsetPolygonAsync(CancellationToken cancellationToken)
    {
        var text = await promptService.PromptAsync("Offset polygon", "Signed offset distance in metres (positive outward)", "10", cancellationToken);
        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out var metres))
        {
            if (text is not null) ShowStatus("Enter a valid offset distance.");
            return;
        }
        var result = polygonService.PreviewOffset(metres);
        if (!result.Succeeded || result.Preview is null) { ShowStatus(result.Message); return; }
        if (await confirmationService.ConfirmAsync("Apply polygon offset", $"Replace the polygon with the {metres:F1} m offset preview?", "Apply", cancellationToken))
            ShowStatus(polygonService.ApplyPreview(result.Preview).Message);
    }

    [RelayCommand]
    private void ShowPolygonArea()
    {
        var area = polygonService.CalculateArea();
        ShowStatus(area is null ? "Create a polygon first." : $"Area: {area.SquareMeters:F1} m² • {area.Hectares:F3} ha • {area.SquareKilometers:F4} km² • {area.Acres:F3} acres • {area.SquareFeet:F0} ft²");
    }

    [RelayCommand]
    private async Task SavePolygonAsync(CancellationToken cancellationToken)
    {
        if (polygonService.Snapshot.Polygon is null) { ShowStatus("Create a polygon first."); return; }
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(polygonService.Serialize(dateTimeProvider.UtcNow)));
        var path = await fileSaveService.SaveAsync("planning-polygon.mppolygon", stream, cancellationToken);
        ShowStatus(path is null ? "Polygon save cancelled." : $"Polygon saved to {path}.");
    }

    [RelayCommand]
    private async Task LoadPolygonAsync(CancellationToken cancellationToken)
    {
        using var file = await fileOpenService.OpenAsync("Open MissionPlanner polygon", cancellationToken: cancellationToken);
        if (file is null) return;
        using var reader = new StreamReader(file.Content, Encoding.UTF8, true, leaveOpen: true);
        var result = polygonService.Deserialize(await reader.ReadToEndAsync(cancellationToken));
        ShowStatus(result.Message);
    }

    [RelayCommand]
    private async Task KmlOverlayAsync(CancellationToken cancellationToken)
    {
        var imported = await OpenGeospatialAsync("Open KML overlay", false, cancellationToken);
        if (imported is null) return;
        importedOverlays = imported.Features.Where(feature => feature.Positions.Count > 0)
            .Select(feature => new ImportedPlanningOverlay(feature.Name, feature.Positions, feature.Kind == GeospatialGeometryKind.Polygon)).ToArray();
        UpdatePlanningOverlay();
        ShowStatus($"Overlay replaced with {importedOverlays.Count} imported features.");
    }

    [RelayCommand]
    private void ClearImportedOverlay()
    {
        importedOverlays = [];
        UpdatePlanningOverlay();
        ShowStatus("Imported overlay cleared.");
    }

    [RelayCommand]
    private async Task LoadKmlFileAsync(CancellationToken cancellationToken) =>
        await ImportMissionGeometryAsync(false, cancellationToken);

    [RelayCommand]
    private async Task LoadShapefileAsync(CancellationToken cancellationToken) =>
        await ImportMissionGeometryAsync(true, cancellationToken);

    [RelayCommand]
    private async Task PolygonFromShapefileAsync(CancellationToken cancellationToken)
    {
        var imported = await OpenGeospatialAsync("Open polygon shapefile", true, cancellationToken);
        if (imported is null) return;
        var polygons = imported.Features.Where(feature => feature.Kind == GeospatialGeometryKind.Polygon).ToArray();
        if (polygons.Length == 0) { ShowStatus("The shapefile contains no polygon geometry."); return; }
        var selectedName = polygons.Length == 1 ? polygons[0].Name
            : await choiceService.ChooseAsync("Choose planning polygon", polygons.Select(feature => feature.Name).ToArray(), cancellationToken);
        var selected = polygons.FirstOrDefault(feature => feature.Name == selectedName);
        if (selected is not null) ShowStatus(polygonService.Set(selected.Name, selected.Positions).Message);
    }

    private async Task ImportMissionGeometryAsync(bool shapefile, CancellationToken cancellationToken)
    {
        var imported = await OpenGeospatialAsync(shapefile ? "Open shapefile" : "Open KML/KMZ file", shapefile, cancellationToken);
        if (imported is null) return;
        var preview = imported.Preview;
        var choice = await choiceService.ChooseAsync(
            $"{preview.Points} points, {preview.LineStrings} lines, {preview.Polygons} polygons, {preview.MissionCandidates} waypoint candidates, {preview.Unsupported} unsupported",
            ["Append waypoints", "Replace mission", "Use first polygon as planning polygon"], cancellationToken);
        if (choice == "Use first polygon as planning polygon")
        {
            var polygon = imported.Features.FirstOrDefault(feature => feature.Kind == GeospatialGeometryKind.Polygon);
            ShowStatus(polygon is null ? "No polygon geometry was found." : polygonService.Set(polygon.Name, polygon.Positions).Message);
            return;
        }
        if (choice is not ("Append waypoints" or "Replace mission")) return;
        var candidates = imported.Features.Where(feature => feature.Kind is GeospatialGeometryKind.Point or GeospatialGeometryKind.LineString)
            .SelectMany(feature => feature.Positions.Select(position => (Position: position, feature.AltitudeMeters))).ToArray();
        if (candidates.Length == 0) { ShowStatus("No point or line geometry can be imported as waypoints."); return; }
        var target = choice == "Replace mission" ? new Mission(MissionId.New(), "Imported mission") : Mission;
        foreach (var candidate in candidates)
            target.Add(new WaypointMissionItem(MissionItemId.New(), 0, candidate.Position,
                new MissionAltitude(candidate.AltitudeMeters ?? DefaultAltitudeMeters, MissionAltitudeReference.Home), TimeSpan.Zero, WaypointRadiusMeters));
        if (!ReferenceEquals(target, Mission)) ReplaceMission(target, $"Mission replaced with {candidates.Length} imported waypoints.");
        else OnMissionChanged($"Appended {candidates.Length} imported waypoints.");
    }

    private async Task<GeospatialImportResult?> OpenGeospatialAsync(string title, bool includeCompanions, CancellationToken cancellationToken)
    {
        using var file = await fileOpenService.OpenAsync(title, cancellationToken: cancellationToken);
        if (file is null) return null;
        await using var memory = new MemoryStream();
        await file.Content.CopyToAsync(memory, cancellationToken);
        var companions = new Dictionary<string, ReadOnlyMemory<byte>>(StringComparer.OrdinalIgnoreCase);
        if (includeCompanions && file.FullPath is { } fullPath)
        {
            foreach (var extension in new[] { ".prj", ".dbf" })
            {
                var path = Path.ChangeExtension(fullPath, extension);
                if (File.Exists(path) && new FileInfo(path).Length <= 16 * 1024 * 1024)
                    companions[extension] = await File.ReadAllBytesAsync(path, cancellationToken);
            }
        }
        if (includeCompanions && !companions.ContainsKey(".prj"))
        {
            var assume = await confirmationService.ConfirmAsync("Missing coordinate system", "No .prj file was found. Treat plausible coordinates as WGS84?", "Use WGS84", cancellationToken);
            if (!assume) return null;
            companions[".prj"] = Encoding.UTF8.GetBytes("GEOGCS[\"WGS 84\",DATUM[\"WGS_1984\"]]");
        }
        var result = geospatialImportService.Import(new GeospatialSource(file.FileName, memory.ToArray(), companions));
        if (!result.Succeeded) { ShowStatus(result.Message); return null; }
        return result;
    }

    [RelayCommand]
    private async Task DownloadFenceAsync(CancellationToken cancellationToken)
    {
        if (activeVehicle.VehicleId is not { } vehicleId) { ShowStatus("Connect a vehicle first."); return; }
        var snapshot = fenceService.GetSnapshot(vehicleId);
        if (snapshot.IsDirty && !await confirmationService.ConfirmAsync("Replace local fence", "Downloading replaces local fence edits and retains a backup.", "Download", cancellationToken)) return;
        var report = await fenceService.DownloadAsync(vehicleId, true, FenceProgress(), cancellationToken);
        ShowFenceReport(report);
    }

    [RelayCommand]
    private async Task UploadFenceAsync(CancellationToken cancellationToken)
    {
        if (activeVehicle.VehicleId is not { } vehicleId) { ShowStatus("Connect a vehicle first."); return; }
        var plan = fenceService.GetSnapshot(vehicleId).LocalPlan;
        var inclusions = plan.Areas.Count(area => area.Kind is FenceAreaKind.PolygonInclusion or FenceAreaKind.CircleInclusion);
        var exclusions = plan.Areas.Count - inclusions;
        if (!await confirmationService.ConfirmAsync("Replace vehicle fence", $"Upload {inclusions} inclusion and {exclusions} exclusion areas{(plan.ReturnPoint is null ? string.Empty : " with a return point")}?", "Validate and upload", cancellationToken)) return;
        var session = await fenceService.OpenParameterSessionAsync(vehicleId, cancellationToken);
        ShowFenceReport(await fenceService.ApplyAsync(vehicleId, session, FenceProgress(), cancellationToken));
    }

    [RelayCommand]
    private void SetFenceReturnLocation() => BeginInteraction(MissionMapInteractionMode.SetFenceReturnLocation, "Click the map to set the local fence return location.");

    [RelayCommand]
    private async Task SaveFenceAsync(CancellationToken cancellationToken)
    {
        if (activeVehicle.VehicleId is not { } vehicleId) { ShowStatus("Select a vehicle workspace first."); return; }
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(fenceFileCodec.Serialize(fenceService.GetSnapshot(vehicleId).LocalPlan)));
        var path = await fileSaveService.SaveAsync("mission-fence.mpfence", stream, cancellationToken);
        ShowStatus(path is null ? "Fence save cancelled." : $"Fence saved to {path}.");
    }

    [RelayCommand]
    private async Task LoadFenceAsync(CancellationToken cancellationToken)
    {
        if (activeVehicle.VehicleId is not { } vehicleId) { ShowStatus("Select a vehicle workspace first."); return; }
        var current = fenceService.GetSnapshot(vehicleId);
        if (current.IsDirty && !await confirmationService.ConfirmAsync("Replace local fence", "Loading replaces current local edits.", "Load", cancellationToken)) return;
        using var file = await fileOpenService.OpenAsync("Open MissionPlanner fence", cancellationToken: cancellationToken);
        if (file is null) return;
        using var reader = new StreamReader(file.Content, Encoding.UTF8, true, leaveOpen: true);
        try { fenceService.SetLocalPlan(vehicleId, fenceFileCodec.Deserialize(await reader.ReadToEndAsync(cancellationToken))); ShowStatus("Fence loaded locally; upload to apply it."); }
        catch (InvalidDataException exception) { ShowStatus(exception.Message); }
    }

    [RelayCommand]
    private async Task ClearFenceAsync(CancellationToken cancellationToken)
    {
        if (activeVehicle.VehicleId is not { } vehicleId) { ShowStatus("Select a vehicle workspace first."); return; }
        var choice = await choiceService.ChooseAsync("Clear Geo-Fence", ["Clear local plan only", "Clear vehicle fence"], cancellationToken);
        if (choice == "Clear local plan only") { fenceService.SetLocalPlan(vehicleId, FencePlan.Empty); ShowStatus("Local fence cleared; vehicle unchanged."); }
        else if (choice == "Clear vehicle fence" && await confirmationService.ConfirmAsync("Clear vehicle fence", "This removes all fence geometry from the vehicle after retaining a backup.", "Clear vehicle", cancellationToken))
            ShowFenceReport(await fenceService.ClearAsync(vehicleId, cancellationToken));
    }

    private IProgress<FenceTransferProgress> FenceProgress() => new Progress<FenceTransferProgress>(progress =>
        ShowStatus($"{progress.Stage}: {progress.Completed}/{progress.Total}"));

    private void ShowFenceReport(FenceOperationReport report) => ShowStatus(report.Validation.IsValid
        ? report.Message : string.Join(Environment.NewLine, report.Validation.Issues.Select(issue => issue.Message)));

    private static IReadOnlyList<GeoPosition> FenceOutline(FencePlan plan)
    {
        var area = plan.Areas.FirstOrDefault();
        if (area is null) return plan.ReturnPoint is { } point ? [point] : [];
        if (area.Vertices.Count > 0) return area.Vertices;
        if (area.Center is not { } center || area.RadiusMeters <= 0) return [];
        const double earthRadius = 6378137d;
        return Enumerable.Range(0, 36).Select(index =>
        {
            var angle = index * Math.PI * 2d / 36d;
            var latitude = center.LatitudeDegrees + area.RadiusMeters * Math.Cos(angle) / earthRadius * 180d / Math.PI;
            var longitude = center.LongitudeDegrees + area.RadiusMeters * Math.Sin(angle) / (earthRadius * Math.Cos(center.LatitudeDegrees * Math.PI / 180d)) * 180d / Math.PI;
            return new GeoPosition(latitude, longitude);
        }).ToArray();
    }

    [RelayCommand]
    private async Task SetRallyPointAsync(CancellationToken cancellationToken)
    {
        var altitudeText = await promptService.PromptAsync("Set rally point", "Altitude in metres", DefaultAltitudeMeters.ToString("F0", CultureInfo.CurrentCulture), cancellationToken);
        if (!double.TryParse(altitudeText, NumberStyles.Float, CultureInfo.CurrentCulture, out var altitude)) { if (altitudeText is not null) ShowStatus("Enter a valid rally altitude."); return; }
        var reference = await choiceService.ChooseAsync("Rally altitude reference", ["Relative to home", "Mean sea level", "Terrain"], cancellationToken);
        if (reference is null) return;
        pendingRallyAltitude = new MissionAltitude(altitude, reference switch { "Mean sea level" => MissionAltitudeReference.MeanSeaLevel, "Terrain" => MissionAltitudeReference.Terrain, _ => MissionAltitudeReference.Home });
        BeginInteraction(MissionMapInteractionMode.SetRallyPoint, "Click the map to add the local rally point.");
    }

    [RelayCommand]
    private async Task DownloadRallyAsync(CancellationToken cancellationToken)
    {
        if (activeVehicle.VehicleId is not { } vehicleId) { ShowStatus("Connect a vehicle first."); return; }
        var snapshot = rallyService.GetSnapshot(vehicleId);
        if (snapshot.IsDirty && !await confirmationService.ConfirmAsync("Replace local rally points", "Downloading replaces local rally edits.", "Download", cancellationToken)) return;
        ShowStatus((await rallyService.DownloadAsync(vehicleId, true, cancellationToken)).Message);
    }

    [RelayCommand]
    private async Task UploadRallyAsync(CancellationToken cancellationToken)
    {
        if (activeVehicle.VehicleId is not { } vehicleId) { ShowStatus("Connect a vehicle first."); return; }
        var count = rallyService.GetSnapshot(vehicleId).LocalPlan.Points.Count;
        if (await confirmationService.ConfirmAsync("Replace vehicle rally points", $"Upload {count} local rally points as a separate MAVLink rally plan?", "Upload", cancellationToken))
            ShowStatus((await rallyService.UploadAsync(vehicleId, cancellationToken)).Message);
    }

    [RelayCommand]
    private async Task ClearRallyAsync(CancellationToken cancellationToken)
    {
        if (activeVehicle.VehicleId is not { } vehicleId) { ShowStatus("Select a vehicle workspace first."); return; }
        var choice = await choiceService.ChooseAsync("Clear rally points", ["Clear local plan only", "Clear vehicle rally points"], cancellationToken);
        if (choice == "Clear local plan only") { rallyService.SetLocalPlan(vehicleId, RallyPlan.Empty); ShowStatus("Local rally points cleared; vehicle unchanged."); }
        else if (choice == "Clear vehicle rally points" && await confirmationService.ConfirmAsync("Clear vehicle rally points", "This removes all rally points from the vehicle.", "Clear vehicle", cancellationToken))
            ShowStatus((await rallyService.ClearVehicleAsync(vehicleId, cancellationToken)).Message);
    }

    [RelayCommand]
    private async Task SaveRallyAsync(CancellationToken cancellationToken)
    {
        if (activeVehicle.VehicleId is not { } vehicleId) { ShowStatus("Select a vehicle workspace first."); return; }
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(rallyFileCodec.Serialize(rallyService.GetSnapshot(vehicleId).LocalPlan, dateTimeProvider.UtcNow)));
        var path = await fileSaveService.SaveAsync("rally-points.mprally", stream, cancellationToken);
        ShowStatus(path is null ? "Rally save cancelled." : $"Rally points saved to {path}.");
    }

    [RelayCommand]
    private async Task LoadRallyAsync(CancellationToken cancellationToken)
    {
        if (activeVehicle.VehicleId is not { } vehicleId) { ShowStatus("Select a vehicle workspace first."); return; }
        using var file = await fileOpenService.OpenAsync("Open MissionPlanner rally plan", cancellationToken: cancellationToken);
        if (file is null) return;
        using var reader = new StreamReader(file.Content, Encoding.UTF8, true, leaveOpen: true);
        try { rallyService.SetLocalPlan(vehicleId, rallyFileCodec.Deserialize(await reader.ReadToEndAsync(cancellationToken))); ShowStatus("Rally points loaded locally; upload to apply them."); }
        catch (InvalidDataException exception) { ShowStatus(exception.Message); }
    }

    [RelayCommand]
    private async Task CreateWaypointCircleAsync(CancellationToken cancellationToken) => await CreateCircleAsync(false, cancellationToken);

    [RelayCommand]
    private async Task CreateSplineCircleAsync(CancellationToken cancellationToken) => await CreateCircleAsync(true, cancellationToken);

    private async Task CreateCircleAsync(bool spline, CancellationToken cancellationToken)
    {
        if (TargetPosition() is not { } center) { ShowStatus("Select a map position for the circle center."); return; }
        var radiusText = await promptService.PromptAsync("Generate circle", "Radius in metres", "100", cancellationToken);
        var countText = await promptService.PromptAsync("Generate circle", "Number of points (3-1000)", "12", cancellationToken);
        if (!double.TryParse(radiusText, NumberStyles.Float, CultureInfo.CurrentCulture, out var radius)
            || !int.TryParse(countText, NumberStyles.Integer, CultureInfo.CurrentCulture, out var count)) { ShowStatus("Enter a valid radius and point count."); return; }
        var direction = await choiceService.ChooseAsync("Circle direction", ["Clockwise", "Counter-clockwise"], cancellationToken);
        if (direction is null) return;
        double? endAltitude = null;
        if (spline)
        {
            var endText = await promptService.PromptAsync("Spline circle", "End altitude in metres (for deterministic helical climb)", DefaultAltitudeMeters.ToString("F0", CultureInfo.CurrentCulture), cancellationToken);
            if (!double.TryParse(endText, NumberStyles.Float, CultureInfo.CurrentCulture, out var parsed)) return;
            endAltitude = parsed;
        }
        await PreviewAndApplyGeneratedAsync(autoWaypointGenerator.GenerateCircle(new(center, radius, count, direction == "Clockwise", 0,
            DefaultAltitude(), endAltitude, spline, false)), cancellationToken);
    }

    [RelayCommand]
    private void AutoWaypointArea() => ShowPolygonArea();

    [RelayCommand]
    private async Task AutoWaypointTextAsync(CancellationToken cancellationToken)
    {
        if (TargetPosition() is not { } origin) { ShowStatus("Select a map position for the text origin."); return; }
        var text = await promptService.PromptAsync("Waypoint text", "Text (supported stroke-font letters/digits, maximum 32)", "HOME", cancellationToken);
        var heightText = await promptService.PromptAsync("Waypoint text", "Character height in metres", "50", cancellationToken);
        if (text is null || !double.TryParse(heightText, NumberStyles.Float, CultureInfo.CurrentCulture, out var height)) return;
        await PreviewAndApplyGeneratedAsync(autoWaypointGenerator.GenerateText(new(text, origin, height, 0, .3, DefaultAltitude())), cancellationToken);
    }

    private async Task PreviewAndApplyGeneratedAsync(AutoWaypointGenerationResult result, CancellationToken cancellationToken)
    {
        if (!result.Succeeded) { ShowStatus(result.Message); return; }
        generatedPreview = result.PreviewPositions; UpdatePlanningOverlay();
        var choice = await choiceService.ChooseAsync($"Preview: {result.Items.Count} generated mission items", ["Append to mission", "Replace mission"], cancellationToken);
        if (choice is not ("Append to mission" or "Replace mission")) { generatedPreview = []; UpdatePlanningOverlay(); return; }
        var target = choice == "Replace mission" ? new Mission(MissionId.New(), "Generated mission") : Mission;
        foreach (var item in result.Items) target.Add(item);
        generatedPreview = []; UpdatePlanningOverlay();
        if (ReferenceEquals(target, Mission)) OnMissionChanged($"Appended {result.Items.Count} generated mission items.");
        else ReplaceMission(target, $"Mission replaced with {result.Items.Count} generated mission items.");
    }

    [RelayCommand]
    private async Task CreateCircleSurveyAsync(CancellationToken cancellationToken)
    {
        if (TargetPosition() is not { } center) { ShowStatus("Select a circle-survey center."); return; }
        var outerText = await promptService.PromptAsync("Circle survey", "Outer radius in metres", "200", cancellationToken);
        var spacingText = await promptService.PromptAsync("Circle survey", "Radial spacing in metres", "50", cancellationToken);
        if (!double.TryParse(outerText, NumberStyles.Float, CultureInfo.CurrentCulture, out var outer)
            || !double.TryParse(spacingText, NumberStyles.Float, CultureInfo.CurrentCulture, out var spacing)) return;
        var result = surveyMissionGenerator.GenerateCircle(new(center, spacing, outer, spacing, 24, true, DefaultAltitude()));
        await PreviewAndApplySurveyAsync(result, cancellationToken);
    }

    [RelayCommand]
    private async Task CreateGridSurveyAsync(CancellationToken cancellationToken)
    {
        if (polygonService.Snapshot.Polygon is not { } polygon) { ShowStatus("Create or load a planning polygon first."); return; }
        var spacingText = await promptService.PromptAsync("Grid survey", "Flight-line spacing in metres", "30", cancellationToken);
        var angleText = await promptService.PromptAsync("Grid survey", "Flight-line angle in degrees", "0", cancellationToken);
        if (!double.TryParse(spacingText, NumberStyles.Float, CultureInfo.CurrentCulture, out var spacing)
            || !double.TryParse(angleText, NumberStyles.Float, CultureInfo.CurrentCulture, out var angle)) return;
        var cross = await confirmationService.ConfirmAsync("Cross-grid", "Add a perpendicular second grid?", "Cross-grid", cancellationToken);
        await PreviewAndApplySurveyAsync(surveyMissionGenerator.GenerateGrid(new(polygon, angle, spacing, 0, DefaultAltitude(), cross)), cancellationToken);
    }

    private async Task PreviewAndApplySurveyAsync(SurveyMissionResult result, CancellationToken cancellationToken)
    {
        if (!result.Succeeded) { ShowStatus(result.Message); return; }
        if (result.Statistics is { } statistics) ShowStatus($"Preview: {statistics.LineCount} legs, {statistics.PointCount} points, {statistics.DistanceMeters:F0} m, {statistics.AreaSquareMeters:F0} m².");
        await PreviewAndApplyGeneratedAsync(new(true, result.Message, result.Items, result.Preview), cancellationToken);
    }

    [RelayCommand]
    private void MeasureDistance() => BeginInteraction(MissionMapInteractionMode.MeasureDistance, "Click two map points to measure geodesic distance and initial bearing.");

    [RelayCommand]
    private async Task RotateMapAsync(CancellationToken cancellationToken)
    {
        var text = await promptService.PromptAsync("Rotate map", "Bearing degrees (0-359; 0 resets north)", "0", cancellationToken);
        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out var angle) || !double.IsFinite(angle)) { if (text is not null) ShowStatus("Enter a valid bearing."); return; }
        angle = ((angle % 360) + 360) % 360; MapRotationRequested?.Invoke(this, angle); ShowStatus($"Map rotation set to {angle:F0}° for this session.");
    }

    [RelayCommand]
    private async Task PrefetchVisibleAsync(CancellationToken cancellationToken)
    {
        var center = TargetPosition() ?? HomePosition;
        if (center is null) { ShowStatus("Select a map position before visible-area prefetch."); return; }
        await RunPrefetchAsync([new(center.Value.LatitudeDegrees - .02, center.Value.LongitudeDegrees - .03, center.Value.LatitudeDegrees + .02, center.Value.LongitudeDegrees + .03)], cancellationToken);
    }

    [RelayCommand]
    private async Task PrefetchWaypointPathAsync(CancellationToken cancellationToken)
    {
        var positions = Mission.Items.Select(PositionOf).OfType<GeoPosition>().ToArray();
        if (positions.Length == 0) { ShowStatus("The mission has no positioned route to prefetch."); return; }
        const double corridor = .005;
        var areas = positions.Zip(positions.Skip(1), (a, b) => new MapPrefetchBounds(Math.Min(a.LatitudeDegrees, b.LatitudeDegrees) - corridor,
            Math.Min(a.LongitudeDegrees, b.LongitudeDegrees) - corridor, Math.Max(a.LatitudeDegrees, b.LatitudeDegrees) + corridor,
            Math.Max(a.LongitudeDegrees, b.LongitudeDegrees) + corridor)).ToArray();
        if (areas.Length == 0) areas = [new(positions[0].LatitudeDegrees - corridor, positions[0].LongitudeDegrees - corridor, positions[0].LatitudeDegrees + corridor, positions[0].LongitudeDegrees + corridor)];
        await RunPrefetchAsync(areas, cancellationToken);
    }

    private async Task RunPrefetchAsync(IReadOnlyList<MapPrefetchBounds> areas, CancellationToken cancellationToken)
    {
        var request = new MapPrefetchRequest(SelectedSourceId, areas, 12, 15);
        var estimate = await mapTilePrefetchService.EstimateAsync(request, cancellationToken);
        if (!estimate.IsAllowed) { ShowStatus(estimate.Message); return; }
        if (!await confirmationService.ConfirmAsync("Warm online map cache", $"Fetch {estimate.TileCount} tiles at zoom {estimate.MinimumZoom}-{estimate.MaximumZoom}? This does not create an offline pack.", "Start", cancellationToken)) return;
        var progress = new Progress<(int Completed, int Total)>(value => ShowStatus($"Prefetch: {value.Completed}/{value.Total}"));
        ShowStatus((await mapTilePrefetchService.PrefetchAsync(request, progress, cancellationToken)).Message);
    }

    [RelayCommand]
    private async Task ShowElevationGraphAsync(CancellationToken cancellationToken)
    {
        ShowStatus("Sampling mission terrain profile…");
        ElevationProfile = await elevationProfileService.GenerateAsync(new(Mission, HomePosition, null), cancellationToken);
        IsElevationProfileVisible = ElevationProfile.Samples.Count > 0;
        ShowStatus(IsElevationProfileVisible
            ? $"Elevation profile: {ElevationProfile.Samples.Count} samples, {ElevationProfile.UnavailableSamples} terrain gaps. Relative-to-home clearance requires home MSL altitude."
            : "Add at least two geographic mission items to create an elevation profile.");
    }

    [RelayCommand]
    private void CloseElevationGraph() => IsElevationProfileVisible = false;

    [RelayCommand]
    private async Task AddPoiAsync(CancellationToken cancellationToken)
    {
        if (TargetPosition() is not { } position) { ShowStatus("Select a map position for the POI."); return; }
        var name = await promptService.PromptAsync("Add point of interest", "Name", $"POI {poiService.Snapshot.Items.Count + 1}", cancellationToken);
        if (string.IsNullOrWhiteSpace(name)) return;
        var description = await promptService.PromptAsync("Add point of interest", "Optional description", null, cancellationToken);
        await poiService.AddAsync(name, position, PointerAltitude, description, null, cancellationToken);
        ShowStatus($"Local POI '{name}' saved.");
    }

    [RelayCommand]
    private async Task EditPoiAsync(CancellationToken cancellationToken)
    {
        if (TargetPosition() is not { } position || poiService.FindNearest(position) is not { } item) { ShowStatus("No POI is available to edit."); return; }
        var name = await promptService.PromptAsync("Edit nearest POI", $"Name ({item.Name})", item.Name, cancellationToken);
        if (string.IsNullOrWhiteSpace(name)) return;
        var description = await promptService.PromptAsync("Edit nearest POI", "Description", item.Description, cancellationToken);
        await poiService.UpdateAsync(item with { Name = name, Description = description }, cancellationToken);
        ShowStatus($"Local POI '{name}' updated.");
    }

    [RelayCommand]
    private async Task DeletePoiAsync(CancellationToken cancellationToken)
    {
        if (TargetPosition() is not { } position || poiService.FindNearest(position) is not { } item) { ShowStatus("No POI is available to delete."); return; }
        if (!await confirmationService.ConfirmAsync("Delete nearest POI", $"Delete local POI '{item.Name}'?", "Delete", cancellationToken)) return;
        await poiService.DeleteAsync(item.Id, cancellationToken); ShowStatus($"Local POI '{item.Name}' deleted.");
    }

    [RelayCommand]
    private async Task SetTrackerHomeAsync(CancellationToken cancellationToken)
    {
        if (TargetPosition() is not { } position) { ShowStatus("Select a map position for tracker home."); return; }
        var text = await promptService.PromptAsync("Tracker home", "Optional altitude in metres", PointerAltitude?.ToString("F1", CultureInfo.CurrentCulture), cancellationToken);
        double? altitude = null;
        if (!string.IsNullOrWhiteSpace(text))
        {
            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out var parsed)) { ShowStatus("Enter a valid altitude or leave it empty."); return; }
            altitude = parsed;
        }
        trackerHomeService.Set(position, altitude, dateTimeProvider.UtcNow, "Mission map context");
        ShowStatus("Local tracker home updated. No antenna-tracker hardware command was sent.");
    }

    [RelayCommand]
    private async Task EnterUtmCoordinateAsync(CancellationToken cancellationToken)
    {
        var text = await promptService.PromptAsync("Enter UTM coordinate", "Format: zone+hemisphere easting northing (example: 32N 500000 6170000)", "32N 500000 6170000", cancellationToken);
        if (text is null) return;
        GeographicCoordinate geographic; try { geographic = geodeticConverter.ToGeographic(geodeticConverter.ParseUtm(text)); }
        catch (Exception exception) when (exception is FormatException or ArgumentOutOfRangeException) { ShowStatus(exception.Message); return; }
        var position = new GeoPosition(geographic.Latitude, geographic.Longitude); ContextPosition = position;
        var choice = await choiceService.ChooseAsync($"Converted to {geographic.Latitude:F7}, {geographic.Longitude:F7}", ["Add waypoint here", "Center map here"], cancellationToken);
        if (choice == "Add waypoint here") AddWaypoint(position, "Waypoint added from UTM coordinate.");
        else if (choice == "Center map here") { MapCenterRequested?.Invoke(this, position); ShowStatus("Map centered on converted UTM coordinate."); }
    }

    /// <summary>Calculates great-circle distance and initial bearing between two WGS84 positions.</summary>
    public static (double Distance, double Bearing) CalculateDistanceAndBearing(GeoPosition first, GeoPosition second)
    {
        const double radius = 6371008.8; var lat1 = first.LatitudeDegrees * Math.PI / 180d; var lat2 = second.LatitudeDegrees * Math.PI / 180d;
        var deltaLat = lat2 - lat1; var deltaLon = (second.LongitudeDegrees - first.LongitudeDegrees) * Math.PI / 180d;
        var a = Math.Sin(deltaLat / 2) * Math.Sin(deltaLat / 2) + Math.Cos(lat1) * Math.Cos(lat2) * Math.Sin(deltaLon / 2) * Math.Sin(deltaLon / 2);
        var distance = radius * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        var bearing = (Math.Atan2(Math.Sin(deltaLon) * Math.Cos(lat2), Math.Cos(lat1) * Math.Sin(lat2) - Math.Sin(lat1) * Math.Cos(lat2) * Math.Cos(deltaLon)) * 180d / Math.PI + 360d) % 360d;
        return (distance, bearing);
    }

    [RelayCommand]
    private void InsertWaypointAtVehicle()
    {
        var position = new GeoPosition(VehicleLatitude, VehicleLongitude);
        if (!position.IsValid || (VehicleLatitude == 0 && VehicleLongitude == 0))
        {
            ShowStatus("Vehicle position is not available.");
            return;
        }

        AddWaypoint(position, $"Waypoint {Mission.Items.Count + 1} added at vehicle.");
    }

    [RelayCommand]
    private void DeleteWaypoint()
    {
        var item = NearestItem(ContextPosition) ?? Mission.Items.LastOrDefault();
        if (item is null)
        {
            ShowStatus("Mission is empty.");
            return;
        }

        Mission.Remove(item.Id);
        OnMissionChanged($"Removed item {item.Sequence + 1} ({item.Command}).");
    }

    [RelayCommand]
    private void RemoveItem(MissionItemRow row)
    {
        if (Mission.Remove(row.Id))
        {
            OnMissionChanged($"Removed item {row.Number} ({row.SelectedCommand}).");
        }
    }

    [RelayCommand]
    private void MoveItemUp(MissionItemRow row)
    {
        MoveItem(row, -1);
        OnMissionChanged($"MoveItemUp");
    }

    [RelayCommand]
    private void MoveItemDown(MissionItemRow row)
    {
        MoveItem(row, +1);
        OnMissionChanged($"MoveItemDown");
    }

    [RelayCommand]
    private void LoiterForever()
    {
        AddLoiter(null, null);
    }

    [RelayCommand]
    private async Task LoiterTimeAsync()
    {
        var seconds = await PromptAsync("Loiter Time", "Time to loiter (seconds)", "30");
        if (seconds is null)
        {
            return;
        }

        AddLoiter(TimeSpan.FromSeconds(seconds.Value), null);
    }

    [RelayCommand]
    private async Task LoiterCirclesAsync()
    {
        var turns = await PromptAsync("Loiter Circles", "Number of circles", "3");
        if (turns is null)
        {
            return;
        }

        AddLoiter(null, turns.Value);
    }

    [RelayCommand]
    private void AddReturnToLaunch()
    {
        Mission.Add(new ReturnToLaunchMissionItem(MissionItemId.New(), 0));
        OnMissionChanged("RTL added.");
    }

    [RelayCommand]
    private void AddLand()
    {
        if (TargetPosition() is not { } position)
        {
            ShowStatus("No map position selected.");
            return;
        }

        Mission.Add(new LandMissionItem(MissionItemId.New(), 0, position, new MissionAltitude(0, MissionAltitudeReference.Home)));
        OnMissionChanged("Land added.");
    }

    [RelayCommand]
    private async Task AddTakeoffAsync()
    {
        var altitude = await PromptAsync("Takeoff", "Takeoff altitude (meters)", DefaultAltitudeMeters.ToString(CultureInfo.CurrentCulture));
        if (altitude is null)
        {
            return;
        }

        Mission.Add(new TakeoffMissionItem(MissionItemId.New(), 0, null, new MissionAltitude(altitude.Value, MissionAltitudeReference.Home)));
        OnMissionChanged("Takeoff added.");
    }

    [RelayCommand]
    private void ClearMission()
    {
        ClearMissionData();
    }

    [RelayCommand]
    private void ReverseWaypoints()
    {
        if (Mission.Items.Count < 2)
        {
            ShowStatus("Nothing to reverse.");
            return;
        }

        var reversed = new Mission(Mission.Id, Mission.Name, Mission.Type);
        foreach (var item in Mission.Items.Reverse())
        {
            reversed.Add(item);
        }

        Mission = reversed;
        OnMissionChanged("Waypoints reversed.");
    }

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
    private async Task ModifyAltitudeAsync()
    {
        var altitude = await PromptAsync("Modify Alt", "New altitude for all mission items (meters)", DefaultAltitudeMeters.ToString(CultureInfo.CurrentCulture));
        if (altitude is null)
        {
            return;
        }

        DefaultAltitudeMeters = altitude.Value;
        var newAltitude = new MissionAltitude(altitude.Value, MissionAltitudeReference.Home);
        foreach (var item in Mission.Items.ToList())
        {
            MissionItem? replacement = item switch
            {
                WaypointMissionItem x => x with { Altitude = newAltitude },
                TakeoffMissionItem x => x with { Altitude = newAltitude },
                LoiterMissionItem x => x with { Altitude = newAltitude },
                SplineWaypointMissionItem x => x with { Altitude = newAltitude },
                RoiLocationMissionItem x => x with { Altitude = newAltitude },
                var _ => null
            };
            if (replacement is not null)
            {
                Mission.Replace(item.Id, replacement);
            }
        }

        OnMissionChanged($"Altitude set to {altitude.Value:F0} m.");
    }

    [RelayCommand]
    private async Task SaveWpFileAsync(CancellationToken cancellationToken)
    {
        if (Mission.Items.Count == 0)
        {
            ShowStatus("Mission is empty.");
            return;
        }

        try
        {
            var (format, extension) = await PickSaveFormatAsync();
            if (format is null || extension is null)
            {
                return;
            }

            var content = fileCodec.Build(Mission, HomePosition, format.Value);
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
            var result = await fileSaver.SaveAsync($"{Mission.Name}{extension}", stream, cancellationToken);
            ShowStatus(result.IsSuccessful ? $"Mission saved to {result.FilePath}." : "Save cancelled.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to save mission file");
            ShowStatus($"Save failed: {ex.Message}");
        }

        DirtyMissionItems.Clear();
    }

    private static async Task<(MissionFileFormat? Format, string? Extension)> PickSaveFormatAsync()
    {
        var page = Application.Current?.Windows.FirstOrDefault()?.Page;
        if (page is null)
        {
            return (MissionFileFormat.QgcWpl, ".waypoints");
        }

        var choice = await page.DisplayActionSheetAsync("Save mission as", "Cancel", null, "Waypoints (.waypoints)", "Text (.txt)", "Mission JSON (.mission)");

        return choice switch
        {
            "Waypoints (.waypoints)" => (MissionFileFormat.QgcWpl, ".waypoints"),
            "Text (.txt)" => (MissionFileFormat.QgcWpl, ".txt"),
            "Mission JSON (.mission)" => (MissionFileFormat.MissionJson, ".mission"),
            var _ => (null, null)
        };
    }

    [RelayCommand]
    private async Task LoadWpFileAsync()
    {
        await LoadMissionFileAsync(false);
    }

    [RelayCommand]
    private async Task LoadAndAppendAsync()
    {
        await LoadMissionFileAsync(true);
    }

    [RelayCommand]
    private void NotImplemented(string feature)
    {
        ShowStatus($"{feature} is not implemented yet.");
    }

    [RelayCommand]
    private async Task CloseAsync(CancellationToken cancellationToken)
    {
        await domainEventHub.PublishDomainEventAsync(new EditorDisplayEvent("EditorClose"), cancellationToken);
    }


    private async Task LoadMissionFileAsync(bool append)
    {
        try
        {
            var file = await FilePicker.Default.PickAsync(new PickOptions { PickerTitle = "Select mission file (.waypoints, .txt, .mission)" });
            if (file is null)
            {
                return;
            }

            await using var stream = await file.OpenReadAsync();
            using var reader = new StreamReader(stream);
            var content = await reader.ReadToEndAsync();
            var parsed = fileCodec.Parse(content);

            if (!append)
            {
                Mission = new Mission(MissionId.New(), parsed.Name ?? Path.GetFileNameWithoutExtension(file.FileName));
                if (parsed.Home is not null)
                {
                    HomePosition = parsed.Home;
                }
            }

            foreach (var item in parsed.Items)
            {
                Mission.Add(item);
            }

            OnMissionChanged(parsed.SkippedItems == 0
                ? $"Loaded {parsed.Items.Count} items from {file.FileName}."
                : $"Loaded {parsed.Items.Count} items from {file.FileName}; skipped {parsed.SkippedItems} unsupported.");

            FitToMissionRequested?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load mission file");
            ShowStatus($"Load failed: {ex.Message}");
        }
    }

    private void MoveItem(MissionItemRow row, int offset)
    {
        var index = Mission.Items.ToList().FindIndex(x => x.Id == row.Id);
        var destination = index + offset;
        if (index < 0 || destination < 0 || destination >= Mission.Items.Count)
        {
            return;
        }

        Mission.Move(row.Id, destination);
        OnMissionChanged($"Moved item {row.Number} {(offset < 0 ? "up" : "down")}.");
    }

    private void AddLoiter(TimeSpan? time, double? turns)
    {
        if (TargetPosition() is not { } position)
        {
            ShowStatus("No map position selected.");
            return;
        }

        Mission.Add(new LoiterMissionItem(MissionItemId.New(), 0, position, DefaultAltitude(), time, turns, LoiterRadiusMeters));
        OnMissionChanged("Loiter added.");
    }

    private void AddWaypoint(GeoPosition position, string message)
    {
        if (!position.IsValid)
        {
            ShowStatus("Waypoint coordinates are invalid.");
            return;
        }

        Mission.Add(new WaypointMissionItem(MissionItemId.New(), 0, position, DefaultAltitude(), TimeSpan.Zero, WaypointRadiusMeters));
        OnMissionChanged(message);
    }

    private GeoPosition? TargetPosition()
    {
        if (ContextPosition is { IsValid: true } context)
        {
            return context;
        }

        var vehicle = new GeoPosition(VehicleLatitude, VehicleLongitude);
        return vehicle.IsValid && (VehicleLatitude != 0 || VehicleLongitude != 0) ? vehicle : null;
    }

    private MissionAltitude DefaultAltitude()
    {
        return new MissionAltitude(DefaultAltitudeMeters, MissionAltitudeReference.Home);
    }

    private MissionItem? NearestItem(GeoPosition? position)
    {
        return position is not { } target
            ? null
            : Mission.Items
                .Select(item => (Item: item, Position: PositionOf(item)))
                .Where(x => x.Position is not null)
                .OrderBy(x => DistanceSquared(x.Position!.Value, target))
                .Select(x => x.Item)
                .FirstOrDefault();
    }

    /// <summary>Extracts the map position of a mission item, if it has one.</summary>
    public static GeoPosition? PositionOf(MissionItem item)
    {
        return item switch
        {
            WaypointMissionItem x => x.Position,
            LandMissionItem x => x.Position,
            LoiterMissionItem x => x.Position,
            TakeoffMissionItem x => x.Position,
            SplineWaypointMissionItem x => x.Position,
            RoiLocationMissionItem x => x.Position,
            var _ => null
        };
    }

    private static MissionAltitude? AltitudeOf(MissionItem item)
    {
        return item switch
        {
            WaypointMissionItem x => x.Altitude,
            LandMissionItem x => x.Altitude,
            LoiterMissionItem x => x.Altitude,
            TakeoffMissionItem x => x.Altitude,
            SplineWaypointMissionItem x => x.Altitude,
            RoiLocationMissionItem x => x.Altitude,
            var _ => null
        };
    }

    private static double DistanceSquared(GeoPosition a, GeoPosition b)
    {
        var dLat = a.LatitudeDegrees - b.LatitudeDegrees;
        var dLon = (a.LongitudeDegrees - b.LongitudeDegrees) * Math.Cos(b.LatitudeDegrees * Math.PI / 180);
        return (dLat * dLat) + (dLon * dLon);
    }

    private void OnMissionChanged(string message)
    {
        RebuildRows();
        UpdateMapSnapshot();
        MissionChanged?.Invoke(this, new MissionEventArgs(message));
        ShowStatus(message);
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

    private void RebuildRows()
    {
        foreach (var row in MissionItems)
        {
            row.Dispose();
        }

        DirtyMissionItems.Clear();
        MissionItems.Clear();

        var previousPosition = HomePosition;
        var previousAltitude = 0.0;
        var totalDistance = 0.0;

        foreach (var item in Mission.Items)
        {
            var position = PositionOf(item);
            var altitude = AltitudeOf(item);
            var protocol = protocolMapper.ToProtocol(item, Mission.Type);

            double? distance = null, azimuth = null, gradient = null;
            if (position is { } current && previousPosition is { } previous)
            {
                var legMeters = GeoMath.ApproximateDistanceMeters(
                    previous.LatitudeDegrees, previous.LongitudeDegrees,
                    current.LatitudeDegrees, current.LongitudeDegrees);
                totalDistance += legMeters;
                distance = legMeters;
                azimuth = BearingDegrees(previous, current);

                if (altitude is { } alt && legMeters > 0.5)
                {
                    gradient = (alt.Meters - previousAltitude) / legMeters * 100.0;
                }
            }

            if (position is not null)
            {
                previousPosition = position;
                previousAltitude = altitude?.Meters ?? previousAltitude;
            }

            var row = new MissionItemRow
            {
                Id = item.Id,
                Number = item.Sequence + 1,
                CommandId = protocol.Command,
                Frame = protocol.Frame,
                AutoContinue = protocol.AutoContinue,
                Param1 = FormatParam(protocol.Param1),
                Param2 = FormatParam(protocol.Param2),
                Param3 = FormatParam(protocol.Param3),
                Param4 = FormatParam(protocol.Param4),
                Latitude = position?.LatitudeDegrees,
                Longitude = position?.LongitudeDegrees,
                Altitude = altitude?.Meters,
                Distance = distance,
                Azimuth = azimuth,
                Gradient = gradient,
                // Set the initial selections before attaching the callback so building rows never applies edits.
                SelectedCommand = CommandNameFor(protocol.Command),
                SelectedFrame = FrameNameFor(protocol.Frame)
            };
            row.AttachNotifications(ApplyRowEdit, EditStateChanged);
            MissionItems.Add(row);
        }

        MissionSummary = Mission.Items.Count == 0
            ? "0 items"
            : $"{Mission.Items.Count} items • {totalDistance:F0} m total";

        var dirtyItems = DirtyMissionItems.Any();
        Debug.Assert(dirtyItems == false);
        CancelRowEditsCommand.NotifyCanExecuteChanged();
        ApplyRowEditsCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// Applies the edited values of a row (params, lat/lon, altitude) back to the mission item.
    /// </summary>
    [RelayCommand(CanExecute = "HasDirtyRows")]
    public void ApplyRowEdits()
    {
        if (HasDirtyRows() == false)
        {
            return;
        }

        var allItems = DirtyMissionItems.ToList();
        foreach (var row in allItems)
        {
            ApplyRowEdit(row);
        }

        CancelRowEditsCommand.NotifyCanExecuteChanged();
        ApplyRowEditsCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    ///  
    /// </summary>
    /// <returns></returns>
    public bool HasDirtyRows()
    {
        var hasDirtyRows = DirtyMissionItems.Any();
        Debug.Print($"HasDirtyRows {hasDirtyRows}");
        return hasDirtyRows;
    }

    private void EditStateChanged(MissionItemRow row)
    {
        if (!DirtyMissionItems.Any(r => r.Equals(row)))
        {
            DirtyMissionItems.Add(row);
        }

        CancelRowEditsCommand.NotifyCanExecuteChanged();
        ApplyRowEditsCommand.NotifyCanExecuteChanged();
        MissionSummary = "EditStateChanged IsDirty: " + HasDirtyRows();
    }

    /// <summary>
    /// Cancels any edits made to the mission items and rebuilds the rows to their original state.
    /// </summary>
    [RelayCommand(CanExecute = "HasDirtyRows")]
    public void CancelRowEdits()
    {
        RebuildRows();
        CancelRowEditsCommand.NotifyCanExecuteChanged();
        ApplyRowEditsCommand.NotifyCanExecuteChanged();
    }

    private void ApplyRowEdit(MissionItemRow row)
    {
        row.Dispose();
        var index = Mission.Items.ToList().FindIndex(x => x.Id == row.Id);
        if (index < 0)
        {
            return;
        }

        try
        {
            var commandId = CommandIdFor(row.SelectedCommand) ?? row.CommandId;
            var frameId = FrameIdFor(row.SelectedFrame) ?? row.Frame;

            var protocolItem = new MavLinkMissionItem(
                (ushort)index,
                frameId,
                commandId,
                false,
                row.AutoContinue,
                ParseParam(row.Param1),
                ParseParam(row.Param2),
                ParseParam(row.Param3),
                // Param4 is yaw/heading where NaN means "not set"; keep an empty cell as NaN.
                ParseParam(row.Param4, float.NaN),
                (int)Math.Round(row.Latitude.HasValue ? row.Latitude.Value * 1e7 : 0.0),
                (int)Math.Round(row.Longitude.HasValue ? row.Longitude.Value * 1e7 : 0.0),
                row.Altitude.HasValue ? (float)row.Altitude.Value : 0.0f,
                MavMissionType.Mission);

            var replacement = protocolMapper.FromProtocol(protocolItem);
            Mission.Replace(row.Id, replacement);
            OnMissionChanged($"Item {row.Number} updated.");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to apply row edit for item {Number}", row.Number);
            ShowStatus($"Edit failed: {ex.Message}");
        }
    }

    private static string FormatParam(float value)
    {
        return float.IsNaN(value) ? string.Empty : value.ToString("0.###", CultureInfo.CurrentCulture);
    }

    private static float ParseParam(string text, float emptyValue = 0f)
    {
        return float.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out var value) ? value : emptyValue;
    }

    private static double ParseCoordinate(string text)
    {
        return double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out var value) ? value : 0.0;
    }

    private static string CommandNameFor(ushort commandId)
    {
        foreach (var (name, id) in commandDefinitions)
        {
            if (id == commandId)
            {
                return name;
            }
        }

        return $"ID {commandId}";
    }

    private static ushort? CommandIdFor(string? commandName)
    {
        foreach (var (name, id) in commandDefinitions)
        {
            if (name == commandName)
            {
                return id;
            }
        }

        return null;
    }

    private static string FrameNameFor(byte frame)
    {
        foreach (var (name, id) in frameDefinitions)
        {
            if (id == frame)
            {
                return name;
            }
        }

        return frame.ToString(CultureInfo.InvariantCulture);
    }

    private static byte? FrameIdFor(string? frameName)
    {
        foreach (var (name, id) in frameDefinitions)
        {
            if (name == frameName)
            {
                return id;
            }
        }

        return null;
    }

    private static double BearingDegrees(GeoPosition from, GeoPosition to)
    {
        var deltaY = to.LatitudeDegrees - from.LatitudeDegrees;
        var deltaX = (to.LongitudeDegrees - from.LongitudeDegrees) * Math.Cos(from.LatitudeDegrees * Math.PI / 180.0);
        var degrees = Math.Atan2(deltaX, deltaY) * 180.0 / Math.PI;
        return degrees < 0 ? degrees + 360.0 : degrees;
    }

    private void ShowStatus(string message)
    {
        dispatcher.Dispatch(() =>
        {
            StatusMessage = message;
            try
            {
                Toast.Make(message).Show();
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Unable to show toast");
            }
        });
    }

    private static async Task<double?> PromptAsync(string title, string message, string initialValue)
    {
        var page = Application.Current?.Windows.FirstOrDefault()?.Page;
        if (page is null)
        {
            return null;
        }

        var input = await page.DisplayPromptAsync(title, message, initialValue: initialValue, keyboard: Keyboard.Numeric);
        return double.TryParse(input, NumberStyles.Float, CultureInfo.CurrentCulture, out var value) ? value : null;
    }
}
