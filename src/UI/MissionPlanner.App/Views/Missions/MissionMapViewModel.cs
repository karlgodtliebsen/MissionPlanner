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
using MissionPlanner.Core.DomainEvents;
using MissionPlanner.Core.Missions.Abstractions;
using MissionPlanner.Core.Missions.Files;
using MissionPlanner.Core.Missions.Models;
using MissionPlanner.Core.Missions.Planning;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Library.DateTime.Domain;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.MavLink.Missions;
using MissionPlanner.Maps.Coordinates;
using MissionPlanner.Maps.Terrain;
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
        IUserPromptService promptService, IUserConfirmationService confirmationService)
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
        interactionService.Changed += OnInteractionChanged;
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

    /// <summary>Gets the current planning interaction instruction.</summary>
    [ObservableProperty]
    public partial string PlanningInteractionPrompt { get; private set; } = string.Empty;

    /// <summary>The mission plan being edited.</summary>
    public Mission Mission { get; private set; } = new(MissionId.New(), "New Mission");

    /// <summary>Raised whenever the mission items change so the views can redraw pins and the route.</summary>
    public event EventHandler? MissionChanged;

    /// <summary>Raised when the map should pan/zoom to show the whole mission (after load or vehicle read).</summary>
    public event EventHandler? FitToMissionRequested;

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

        if (interactionService.AcceptClick(position))
        {
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
            PlanningOverlaySnapshot = interactionService.Overlay;
            PlanningInteractionPrompt = interactionService.State.Prompt;
        });
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
