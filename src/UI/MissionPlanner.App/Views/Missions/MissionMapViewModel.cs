using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MissionPlanner.Core.Missions.Abstractions;
using MissionPlanner.Core.Missions.Models;
using MissionPlanner.MavLink.Missions;

namespace MissionPlanner.App.Views.Missions;

/// <summary>
/// Shared view model for the mission map editor. It tracks the vehicle, owns the mission plan being
/// edited, and exposes the commands behind the map's right-click context menu. It is registered as a
/// singleton so the FlightData map and the Plan screen edit the same mission.
/// </summary>
public partial class MissionMapViewModel : ObservableObject
{
    private readonly IMissionProtocolMapper protocolMapper;
    private readonly IFileSaver fileSaver;
    private readonly ILogger<MissionMapViewModel> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="MissionMapViewModel"/> class.
    /// </summary>
    public MissionMapViewModel(IMissionProtocolMapper protocolMapper, IFileSaver fileSaver, ILogger<MissionMapViewModel> logger)
    {
        this.protocolMapper = protocolMapper;
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
    }

    /// <summary>
    /// Clears the mission being edited and resets it to a new empty mission.   
    /// </summary>
    public void ClearMissionData()
    {
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
    }

    [RelayCommand]
    private void MoveItemDown(MissionItemRow row)
    {
        MoveItem(row, +1);
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
            var content = BuildQgcWplFile();
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
            var result = await fileSaver.SaveAsync($"{Mission.Name}.waypoints", stream, CancellationToken.None);
            ShowStatus(result.IsSuccessful ? $"Mission saved to {result.FilePath}." : "Save cancelled.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to save mission file");
            ShowStatus($"Save failed: {ex.Message}");
        }
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
            var file = await FilePicker.Default.PickAsync(new PickOptions { PickerTitle = "Select waypoint file" });
            if (file is null)
            {
                return;
            }

            await using var stream = await file.OpenReadAsync();
            using var reader = new StreamReader(stream);
            var content = await reader.ReadToEndAsync();
            var (items, home, skipped) = ParseQgcWplFile(content);

            if (!append)
            {
                Mission = new Mission(MissionId.New(), Path.GetFileNameWithoutExtension(file.FileName));
                if (home is not null)
                {
                    HomePosition = home;
                }
            }

            foreach (var item in items)
            {
                Mission.Add(item);
            }

            OnMissionChanged(skipped == 0
                ? $"Loaded {items.Count} items from {file.FileName}."
                : $"Loaded {items.Count} items from {file.FileName}; skipped {skipped} unsupported.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load mission file");
            ShowStatus($"Load failed: {ex.Message}");
        }
    }

    private (List<MissionItem> Items, GeoPosition? Home, int Skipped) ParseQgcWplFile(string content)
    {
        var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (lines.Length == 0 || !lines[0].StartsWith("QGC WPL", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Not a QGC WPL waypoint file.");
        }

        List<MissionItem> items = [];
        GeoPosition? home = null;
        var skipped = 0;

        foreach (var line in lines.Skip(1))
        {
            var fields = line.Split(['\t', ' '], StringSplitOptions.RemoveEmptyEntries);
            if (fields.Length < 12)
            {
                continue;
            }

            var sequence = ushort.Parse(fields[0], CultureInfo.InvariantCulture);
            var latitude = double.Parse(fields[8], CultureInfo.InvariantCulture);
            var longitude = double.Parse(fields[9], CultureInfo.InvariantCulture);

            // Line 0 is the home position by QGC WPL convention.
            if (sequence == 0)
            {
                home = latitude != 0 || longitude != 0 ? new GeoPosition(latitude, longitude) : null;
                continue;
            }

            var protocolItem = new MavLinkMissionItem(
                (ushort)(sequence - 1),
                byte.Parse(fields[2], CultureInfo.InvariantCulture),
                ushort.Parse(fields[3], CultureInfo.InvariantCulture),
                false,
                fields[11] != "0",
                float.Parse(fields[4], CultureInfo.InvariantCulture),
                float.Parse(fields[5], CultureInfo.InvariantCulture),
                float.Parse(fields[6], CultureInfo.InvariantCulture),
                float.Parse(fields[7], CultureInfo.InvariantCulture),
                (int)Math.Round(latitude * 1e7),
                (int)Math.Round(longitude * 1e7),
                float.Parse(fields[10], CultureInfo.InvariantCulture),
                MavMissionType.Mission);

            try
            {
                items.Add(protocolMapper.FromProtocol(protocolItem));
            }
            catch (NotSupportedException)
            {
                skipped++;
            }
        }

        return (items, home, skipped);
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

    private string BuildQgcWplFile()
    {
        var builder = new StringBuilder();
        builder.AppendLine("QGC WPL 110");

        var home = HomePosition
                   ?? Mission.Items.Select(PositionOf).FirstOrDefault(p => p is not null)
                   ?? new GeoPosition(0, 0);
        AppendWplLine(builder, 0, 1, 0, (ushort)MissionCommand.NavigateWaypoint,
            0, 0, 0, 0, home.LatitudeDegrees, home.LongitudeDegrees, 0, 1);

        foreach (var item in Mission.Items)
        {
            var p = protocolMapper.ToProtocol(item, Mission.Type);
            AppendWplLine(builder, p.Sequence + 1, 0, p.Frame, p.Command,
                p.Param1, p.Param2, p.Param3, p.Param4, p.X / 1e7, p.Y / 1e7, p.Z, p.AutoContinue ? 1 : 0);
        }

        return builder.ToString();
    }

    private static void AppendWplLine(StringBuilder builder, int sequence, int current, byte frame, ushort command,
        float p1, float p2, float p3, float p4, double latitude, double longitude, float altitude, int autoContinue)
    {
        builder.AppendLine(string.Join('\t',
            sequence.ToString(CultureInfo.InvariantCulture),
            current.ToString(CultureInfo.InvariantCulture),
            frame.ToString(CultureInfo.InvariantCulture),
            command.ToString(CultureInfo.InvariantCulture),
            p1.ToString("0.########", CultureInfo.InvariantCulture),
            p2.ToString("0.########", CultureInfo.InvariantCulture),
            p3.ToString("0.########", CultureInfo.InvariantCulture),
            p4.ToString("0.########", CultureInfo.InvariantCulture),
            latitude.ToString("0.########", CultureInfo.InvariantCulture),
            longitude.ToString("0.########", CultureInfo.InvariantCulture),
            altitude.ToString("0.######", CultureInfo.InvariantCulture),
            autoContinue.ToString(CultureInfo.InvariantCulture)));
    }

    private void OnMissionChanged(string message)
    {
        RebuildRows();
        MissionChanged?.Invoke(this, EventArgs.Empty);
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
