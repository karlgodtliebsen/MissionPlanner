using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MissionPlanner.Core.Missions.Abstractions;
using MissionPlanner.Core.Missions.Files;
using MissionPlanner.Core.Missions.Models;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.MavLink.Missions;
using MissionPlanner.Core.ConfigTuning.Planner;

namespace MissionPlanner.App.Views.Missions;

/// <summary>
/// Shared view model for the mission map editor. It tracks the vehicle, owns the mission plan being
/// edited, and exposes the commands behind the map's right-click context menu. It is registered as a
/// singleton so the FlightData map and the Plan screen edit the same mission.
/// </summary>
public partial class MissionMapViewModel : ObservableObject
{
    private readonly IMissionFileCodec fileCodec;
    private readonly IMissionProtocolMapper protocolMapper;
    private readonly IFileSaver fileSaver;
    private readonly ILogger<MissionMapViewModel> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="MissionMapViewModel"/> class.
    /// </summary>
    public MissionMapViewModel(
        IMissionFileCodec fileCodec,
        IMissionProtocolMapper protocolMapper,
        IFileSaver fileSaver,
        IPlannerSettingsService settingsService,
        ILogger<MissionMapViewModel> logger)
    {
        this.fileCodec = fileCodec;
        this.protocolMapper = protocolMapper;
        this.fileSaver = fileSaver;
        this.logger = logger;
        SelectedMapType = MapType(settingsService.Current.Map);
    }

    [ObservableProperty] public partial double VehicleLatitude { get; set; }

    [ObservableProperty] public partial double VehicleLongitude { get; set; }

    [ObservableProperty] public partial double VehicleHeading { get; set; }

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

    /// <summary>The name of the tile source rendered by map views bound to this view model.</summary>
    [ObservableProperty]
    public partial string SelectedMapType { get; set; } = "OpenStreetMap";

    /// <summary>When true the waypoint list shows the complete editor (all columns + header inputs).</summary>
    [ObservableProperty]
    public partial bool IsCompleteEditorMode { get; set; }

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

    /// <summary>The tile sources the map views can render.</summary>
    public IReadOnlyList<string> AvailableMapTypes { get; } =
        ["OpenStreetMap", "Esri World Topo", "Esri World Physical", "Esri Shaded Relief", "Esri Dark Gray"];

    private static string MapType(PlannerMapSettings settings) => settings.Provider switch
    {
        PlannerMapProvider.OpenStreetMap => "OpenStreetMap",
        _ => settings.Style switch
        {
            PlannerMapStyle.Physical => "Esri World Physical",
            PlannerMapStyle.ShadedRelief => "Esri Shaded Relief",
            PlannerMapStyle.DarkGray => "Esri Dark Gray",
            _ => "Esri World Topo"
        }
    };

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
        ("DO_CHANGE_SPEED", 178)
    ];

    /// <summary>Altitude frames selectable in the waypoint editor (v1.38 altmode naming).</summary>
    private static readonly (string Name, byte Id)[] frameDefinitions =
    [
        ("Absolute", 0),
        ("Relative", 3),
        ("Terrain", 10)
    ];

    /// <summary>The command names offered by the editor's Command select.</summary>
    public IReadOnlyList<string> CommandOptions { get; } = commandDefinitions.Select(x => x.Name).ToArray();

    /// <summary>The frame names offered by the editor's Frame select.</summary>
    public IReadOnlyList<string> FrameOptions { get; } = frameDefinitions.Select(x => x.Name).ToArray();

    /// <summary>Display rows for the mission items, kept in sync with <see cref="Mission"/>.</summary>
    public ObservableCollection<MissionItemRow> MissionItems { get; } = [];

    /// <summary>
    /// The map position the context menu actions operate on (where the user right-clicked/tapped).
    /// Updated by the view before the menu opens.
    /// </summary>
    public GeoPosition? ContextPosition { get; private set; }

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

    /// <summary>Handles a primary map click according to the active map editing mode.</summary>
    public void HandleMapClick(double latitude, double longitude)
    {
        var position = new GeoPosition(latitude, longitude);
        ContextPosition = position;

        if (!AddWaypointOnMapClick)
        {
            return;
        }

        AddWaypoint(position, "Waypoint added from map click.");
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
    [Obsolete]
    private async Task SaveWpFileAsync()
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
            var result = await fileSaver.SaveAsync($"{Mission.Name}{extension}", stream, CancellationToken.None);
            ShowStatus(result.IsSuccessful ? $"Mission saved to {result.FilePath}." : "Save cancelled.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to save mission file");
            ShowStatus($"Save failed: {ex.Message}");
        }
    }

    [Obsolete]
    private static async Task<(MissionFileFormat? Format, string? Extension)> PickSaveFormatAsync()
    {
        var page = Application.Current?.Windows.FirstOrDefault()?.Page;
        if (page is null)
        {
            return (MissionFileFormat.QgcWpl, ".waypoints");
        }

        var choice = await page.DisplayActionSheet("Save mission as", "Cancel", null,
            "Waypoints (.waypoints)", "Text (.txt)", "Mission JSON (.mission)");

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

        Mission.Add(new LoiterMissionItem(
            MissionItemId.New(),
            0,
            position,
            DefaultAltitude(),
            time,
            turns,
            RadiusMeters: LoiterRadiusMeters));
        OnMissionChanged("Loiter added.");
    }

    private void AddWaypoint(GeoPosition position, string message)
    {
        if (!position.IsValid)
        {
            ShowStatus("Waypoint coordinates are invalid.");
            return;
        }

        Mission.Add(new WaypointMissionItem(
            MissionItemId.New(),
            0,
            position,
            DefaultAltitude(),
            TimeSpan.Zero,
            AcceptanceRadiusMeters: WaypointRadiusMeters));
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
        MissionChanged?.Invoke(this, new MissionEventArgs(message));
        ShowStatus(message);
    }

    private void RebuildRows()
    {
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
            row.AttachSelectionChanged(ApplyRowEdit);

            MissionItems.Add(row);
        }

        MissionSummary = Mission.Items.Count == 0
            ? "0 items"
            : $"{Mission.Items.Count} items • {totalDistance:F0} m total";
    }

    /// <summary>
    /// Applies the edited values of a row (params, lat/lon, altitude) back to the mission item.
    /// </summary>
    [RelayCommand]
    private void ApplyRowEdit(MissionItemRow row)
    {
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
        StatusMessage = message;
        try
        {
            MainThread.BeginInvokeOnMainThread(() => Toast.Make(message).Show());
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Unable to show toast");
        }
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
