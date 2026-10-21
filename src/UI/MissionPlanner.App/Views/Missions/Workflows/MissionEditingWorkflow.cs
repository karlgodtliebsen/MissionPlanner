using System.Globalization;
using MissionPlanner.App.Presentation;
using MissionPlanner.App.Utilities.Dialogs;
using MissionPlanner.Core.Missions;
using MissionPlanner.Core.Missions.Models;
using MissionPlanner.Core.Missions.Planning;

namespace MissionPlanner.App.Views.Missions.Workflows;

/// <summary>Adds, removes, moves and bulk-edits mission items through the map context menu and the item list.</summary>
public interface IMissionEditingWorkflow : IMissionWorkflow
{
    /// <summary>Appends a waypoint at the target position.</summary>
    void InsertWaypoint();

    /// <summary>Appends a spline waypoint at the target position.</summary>
    void InsertSplineWaypoint();

    /// <summary>Appends a DO_JUMP to the mission start after asking for a repeat count.</summary>
    Task JumpToStartAsync(CancellationToken cancellationToken);

    /// <summary>Appends a DO_JUMP to a typed mission row after asking for a repeat count.</summary>
    Task JumpToWaypointAsync(CancellationToken cancellationToken);

    /// <summary>Appends a region-of-interest location at the target position.</summary>
    void SetRoiHere();

    /// <summary>Appends a waypoint at the vehicle position.</summary>
    void InsertWaypointAtVehicle();

    /// <summary>Removes the item nearest the target position, or the last item.</summary>
    void DeleteWaypoint();

    /// <summary>Removes the mission item behind a list row.</summary>
    void RemoveItem(MissionItemRow row);

    /// <summary>Moves the item behind a list row one place earlier.</summary>
    void MoveItemUp(MissionItemRow row);

    /// <summary>Moves the item behind a list row one place later.</summary>
    void MoveItemDown(MissionItemRow row);

    /// <summary>Appends an unlimited loiter at the target position.</summary>
    void LoiterForever();

    /// <summary>Appends a timed loiter at the target position.</summary>
    Task LoiterTimeAsync(CancellationToken cancellationToken);

    /// <summary>Appends a turn-counted loiter at the target position.</summary>
    Task LoiterCirclesAsync();

    /// <summary>Appends a return-to-launch item.</summary>
    void AddReturnToLaunch();

    /// <summary>Appends a land item at the target position.</summary>
    void AddLand();

    /// <summary>Appends a takeoff item after asking for its altitude.</summary>
    Task AddTakeoffAsync();

    /// <summary>Reverses the order of the mission items.</summary>
    void ReverseWaypoints();

    /// <summary>Sets the altitude of every positioned item and the default altitude.</summary>
    Task ModifyAltitudeAsync();
}

/// <inheritdoc cref="IMissionEditingWorkflow" />
public sealed class MissionEditingWorkflow : MissionWorkflowBase, IMissionEditingWorkflow
{
    private readonly IAdvancedMissionItemService advancedMissionItems;
    private readonly IDialogService dialogService;
    private readonly IUserConfirmationService confirmationService;

    /// <summary>Initializes a new instance of the <see cref="MissionEditingWorkflow"/> class.</summary>
    public MissionEditingWorkflow(IAdvancedMissionItemService advancedMissionItems, IDialogService dialogService, IUserConfirmationService confirmationService)
    {
        this.advancedMissionItems = advancedMissionItems;
        this.dialogService = dialogService;
        this.confirmationService = confirmationService;
    }

    /// <inheritdoc />
    public void InsertWaypoint()
    {
        if (TargetPosition() is not { } position)
        {
            ShowStatus("No map position selected.");
            return;
        }

        AddWaypoint(position, $"Waypoint {Mission.Items.Count + 1} added.");
    }

    /// <inheritdoc />
    public void InsertSplineWaypoint()
    {
        if (TargetPosition() is not { } position)
        {
            ShowStatus("No map position selected.");
            return;
        }

        ApplyAdvancedResult(advancedMissionItems.AddSplineWaypoint(Mission, position, DefaultAltitude()), "Spline waypoint added.");
    }

    /// <inheritdoc />
    public async Task JumpToStartAsync(CancellationToken cancellationToken)
    {
        var repeat = await PromptRepeatCountAsync(cancellationToken);
        if (repeat is not null)
        {
            ApplyAdvancedResult(advancedMissionItems.AddJumpToStart(Mission, repeat.Value), "DO_JUMP to mission start added.");
        }
    }

    /// <inheritdoc />
    public async Task JumpToWaypointAsync(CancellationToken cancellationToken)
    {
        var options = AvaloniaDialogService.CreateDialogOptions("Jump to waypoint", "Ok", null);
        var targetText = await dialogService.PromptAsync(options, "Target mission row (1-based)", "1", cancellationToken: cancellationToken);


        if (!ushort.TryParse(targetText, NumberStyles.Integer, CultureInfo.CurrentCulture, out var displayTarget) || displayTarget == 0)
        {
            if (targetText is not null)
            {
                ShowStatus("Enter an existing mission row number.");
            }

            return;
        }

        var repeat = await PromptRepeatCountAsync(cancellationToken);
        if (repeat is not null)
        {
            ApplyAdvancedResult(advancedMissionItems.AddJump(Mission, (ushort)(displayTarget - 1), repeat.Value), $"DO_JUMP to row {displayTarget} added.");
        }
    }

    /// <inheritdoc />
    public void SetRoiHere()
    {
        if (TargetPosition() is not { } position)
        {
            ShowStatus("No map position selected.");
            return;
        }

        ApplyAdvancedResult(advancedMissionItems.AddRoiLocation(Mission, position, DefaultAltitude()), "ROI location added.");
    }

    /// <inheritdoc />
    public void InsertWaypointAtVehicle()
    {
        if (Host.VehiclePosition is not { } position)
        {
            ShowStatus("Vehicle position is not available.");
            return;
        }

        AddWaypoint(position, $"Waypoint {Mission.Items.Count + 1} added at vehicle.");
    }

    /// <inheritdoc />
    public void DeleteWaypoint()
    {
        var item = NearestItem(Host.ContextPosition) ?? Mission.Items.LastOrDefault();
        if (item is null)
        {
            ShowStatus("Mission is empty.");
            return;
        }

        Mission.Remove(item.Id);
        OnMissionChanged($"Removed item {item.Sequence + 1} ({item.Command}).");
    }

    /// <inheritdoc />
    public void RemoveItem(MissionItemRow row)
    {
        if (Mission.Remove(row.Id))
        {
            OnMissionChanged($"Removed item {row.Number} ({row.SelectedCommand}).");
        }
    }

    /// <inheritdoc />
    public void MoveItemUp(MissionItemRow row)
    {
        MoveItem(row, -1);
        OnMissionChanged($"MoveItemUp");
    }

    /// <inheritdoc />
    public void MoveItemDown(MissionItemRow row)
    {
        MoveItem(row, +1);
        OnMissionChanged($"MoveItemDown");
    }

    /// <inheritdoc />
    public void LoiterForever()
    {
        AddLoiter(null, null);
    }

    /// <inheritdoc />
    public async Task LoiterTimeAsync(CancellationToken cancellationToken)
    {
        var options = dialogService.CreateOptions("Loiter Time", "Ok", null);

        var input = await dialogService.PromptAsync(options, "Time to loiter (seconds)", 30, 0, 24 * 60, cancellationToken: cancellationToken);
        if (input is null)
        {
            return;
        }

        AddLoiter(TimeSpan.FromSeconds(input.Value), null);
    }

    /// <inheritdoc />
    public async Task LoiterCirclesAsync()
    {
        var turns = await dialogService.PromptAsync("Loiter Circles", "Number of circles", 3, 0, 100);
        if (turns is null)
        {
            return;
        }

        AddLoiter(null, turns.Value);
    }

    /// <inheritdoc />
    public void AddReturnToLaunch()
    {
        Mission.Add(new ReturnToLaunchMissionItem(MissionItemId.New(), 0));
        OnMissionChanged("RTL added.");
    }

    /// <inheritdoc />
    public void AddLand()
    {
        if (TargetPosition() is not { } position)
        {
            ShowStatus("No map position selected.");
            return;
        }

        Mission.Add(new LandMissionItem(MissionItemId.New(), 0, position, new MissionAltitude(0, MissionAltitudeReference.Home)));
        OnMissionChanged("Land added.");
    }

    /// <inheritdoc />
    public async Task AddTakeoffAsync()
    {
        var altitude = await dialogService.PromptAsync("Takeoff", "Takeoff altitude (meters)", DefaultAltitudeMeters);
        if (altitude is null)
        {
            return;
        }

        Mission.Add(new TakeoffMissionItem(MissionItemId.New(), 0, null, new MissionAltitude(altitude.Value, MissionAltitudeReference.Home)));
        OnMissionChanged("Takeoff added.");
    }

    /// <inheritdoc />
    public void ReverseWaypoints()
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

        Host.SetMission(reversed, "Waypoints reversed.");
    }

    /// <inheritdoc />
    public async Task ModifyAltitudeAsync()
    {
        var altitude = await dialogService.PromptAsync("Modify Alt", "New altitude for all mission items (meters)", DefaultAltitudeMeters);
        if (altitude is null)
        {
            return;
        }

        Host.DefaultAltitudeMeters = altitude.Value;
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

    private async Task<int?> PromptRepeatCountAsync(CancellationToken cancellationToken)
    {
        var text = await dialogService.PromptAsync("DO_JUMP", "Repeat count (use -1 for infinite)", "1");
        if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.CurrentCulture, out var repeat) || repeat < -1)
        {
            if (text is not null)
            {
                ShowStatus("Repeat count must be -1, zero, or positive.");
            }

            return null;
        }

        return repeat == -1 && !await confirmationService.ConfirmAsync("Infinite DO_JUMP", "This jump repeats indefinitely until the flight mode changes. Add it?", "Add", cancellationToken)
            ? null
            : repeat;
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

        Mission.Add(new LoiterMissionItem(MissionItemId.New(), 0, position, DefaultAltitude(), time, turns, Host.LoiterRadiusMeters));
        OnMissionChanged("Loiter added.");
    }

    private MissionItem? NearestItem(GeoPosition? position)
    {
        return position is not { } target
            ? null
            : Mission.Items
                .Select(item => (Item: item, Position: MissionItemGeometry.PositionOf(item)))
                .Where(x => x.Position is not null)
                .OrderBy(x => DistanceSquared(x.Position!.Value, target))
                .Select(x => x.Item)
                .FirstOrDefault();
    }

    private static double DistanceSquared(GeoPosition a, GeoPosition b)
    {
        var dLat = a.LatitudeDegrees - b.LatitudeDegrees;
        var dLon = (a.LongitudeDegrees - b.LongitudeDegrees) * Math.Cos(b.LatitudeDegrees * Math.PI / 180);
        return (dLat * dLat) + (dLon * dLon);
    }
}
