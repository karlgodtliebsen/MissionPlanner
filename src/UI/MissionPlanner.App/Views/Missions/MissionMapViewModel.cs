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

namespace MissionPlanner.App.Views.Missions;

/// <summary>
/// Shared view model for the mission map editor. It tracks the vehicle, owns the mission plan being
/// edited, and exposes the commands behind the map's right-click context menu. It is registered as a
/// singleton so the FlightData map and the Plan screen edit the same mission.
/// </summary>
public partial class MissionMapViewModel : ObservableObject
{
    private readonly IMissionFileCodec fileCodec;
    private readonly IFileSaver fileSaver;
    private readonly ILogger<MissionMapViewModel> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="MissionMapViewModel"/> class.
    /// </summary>
    public MissionMapViewModel(IMissionFileCodec fileCodec, IFileSaver fileSaver, ILogger<MissionMapViewModel> logger)
    {
        this.fileCodec = fileCodec;
        this.fileSaver = fileSaver;
        this.logger = logger;
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

    /// <summary>The tile sources the map views can render.</summary>
    public IReadOnlyList<string> AvailableMapTypes { get; } =
        ["OpenStreetMap", "Esri World Topo", "Esri World Physical", "Esri Shaded Relief", "Esri Dark Gray"];

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

        Mission.Add(new WaypointMissionItem(MissionItemId.New(), 0, position, DefaultAltitude(), TimeSpan.Zero));
        OnMissionChanged($"Waypoint {Mission.Items.Count} added.");
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

        Mission.Add(new WaypointMissionItem(MissionItemId.New(), 0, position, DefaultAltitude(), TimeSpan.Zero));
        OnMissionChanged($"Waypoint {Mission.Items.Count} added at vehicle.");
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
            OnMissionChanged($"Removed item {row.Number} ({row.Command}).");
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

        Mission.Add(new LoiterMissionItem(MissionItemId.New(), 0, position, DefaultAltitude(), time, turns));
        OnMissionChanged("Loiter added.");
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
        foreach (var item in Mission.Items)
        {
            var position = PositionOf(item);
            var altitude = AltitudeOf(item);
            MissionItems.Add(new MissionItemRow(
                item.Id,
                item.Sequence + 1,
                item.Command.ToString(),
                position?.LatitudeDegrees.ToString("F6", CultureInfo.CurrentCulture) ?? string.Empty,
                position?.LongitudeDegrees.ToString("F6", CultureInfo.CurrentCulture) ?? string.Empty,
                altitude?.Meters.ToString("F0", CultureInfo.CurrentCulture) ?? string.Empty));
        }
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
