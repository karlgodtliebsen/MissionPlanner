using FluentAssertions;
using MissionPlanner.App.Presentation;
using MissionPlanner.App.Utilities.Dialogs;
using MissionPlanner.App.Views.Missions;
using MissionPlanner.App.Views.Missions.Workflows;
using MissionPlanner.Core.Missions;
using MissionPlanner.Core.Missions.Models;
using MissionPlanner.Core.Missions.Planning;
using NSubstitute;

namespace MissionPlanner.AvaloniaUI.Tests;

/// <summary>Verifies the mission item editing commands behind the map context menu and item list.</summary>
public sealed class MissionEditingWorkflowTests
{
    private readonly FakeMissionEditorHost host = new();
    private readonly IDialogService dialogs = Substitute.For<IDialogService>();
    private readonly IUserConfirmationService confirmations = Substitute.For<IUserConfirmationService>();
    private readonly MissionEditingWorkflow workflow;

    public MissionEditingWorkflowTests()
    {
        workflow = new MissionEditingWorkflow(new AdvancedMissionItemService(), dialogs, confirmations);
        workflow.Attach(host);
    }

    private static MissionItemRow RowFor(MissionItem item)
    {
        return new MissionItemRow { Id = item.Id, Number = item.Sequence + 1, CommandId = 16, Frame = 3, AutoContinue = true };
    }

    [Fact]
    public void InsertWaypoint_AddsAtTheContextPosition()
    {
        host.ContextPosition = new GeoPosition(56, 10);

        workflow.InsertWaypoint();

        MissionItemGeometry.PositionOf(host.Mission.Items.Single()).Should().Be(new GeoPosition(56, 10));
        host.MissionChanges.Should().Equal("Waypoint 1 added.");
    }

    [Fact]
    public void InsertWaypoint_WithoutAnyPosition_ReportsAndLeavesTheMissionUntouched()
    {
        workflow.InsertWaypoint();

        host.Mission.Items.Should().BeEmpty();
        host.LastStatus.Should().Be("No map position selected.");
    }

    [Fact]
    public void InsertWaypointAtVehicle_UsesTheVehiclePosition_OrReportsMissingFix()
    {
        workflow.InsertWaypointAtVehicle();
        host.LastStatus.Should().Be("Vehicle position is not available.");

        host.VehiclePosition = new GeoPosition(55, 9);
        workflow.InsertWaypointAtVehicle();

        MissionItemGeometry.PositionOf(host.Mission.Items.Single()).Should().Be(new GeoPosition(55, 9));
    }

    [Fact]
    public void DeleteWaypoint_RemovesTheItemNearestTheContextPosition()
    {
        var far = host.AddWaypointItem(10, 10);
        var near = host.AddWaypointItem(56, 10);
        host.ContextPosition = new GeoPosition(56.0001, 10);

        workflow.DeleteWaypoint();

        host.Mission.Items.Select(item => item.Id).Should().Equal(far.Id);
        host.Mission.Items.Select(item => item.Id).Should().NotContain(near.Id);
    }

    [Fact]
    public void DeleteWaypoint_WithoutContext_RemovesTheLastItem()
    {
        var first = host.AddWaypointItem(10, 10);
        host.AddWaypointItem(20, 20);

        workflow.DeleteWaypoint();

        host.Mission.Items.Single().Id.Should().Be(first.Id);
    }

    [Fact]
    public void DeleteWaypoint_EmptyMission_Reports()
    {
        workflow.DeleteWaypoint();

        host.LastStatus.Should().Be("Mission is empty.");
    }

    [Fact]
    public void RemoveItem_RemovesTheRowsItem()
    {
        var item = host.AddWaypointItem(10, 10);

        workflow.RemoveItem(RowFor(item));

        host.Mission.Items.Should().BeEmpty();
        host.MissionChanges.Single().Should().StartWith("Removed item 1");
    }

    [Fact]
    public void MoveItemDown_SwapsTheItemWithItsSuccessor()
    {
        var first = host.AddWaypointItem(10, 10);
        var second = host.AddWaypointItem(20, 20);

        workflow.MoveItemDown(RowFor(first));

        host.Mission.Items.Select(item => item.Id).Should().Equal(second.Id, first.Id);
    }

    [Fact]
    public void MoveItemUp_OnTheFirstItem_KeepsTheOrder()
    {
        var first = host.AddWaypointItem(10, 10);
        var second = host.AddWaypointItem(20, 20);

        workflow.MoveItemUp(RowFor(first));

        host.Mission.Items.Select(item => item.Id).Should().Equal(first.Id, second.Id);
    }

    [Fact]
    public void ReverseWaypoints_ReplacesTheMissionWithTheReversedOrder()
    {
        var first = host.AddWaypointItem(10, 10);
        var second = host.AddWaypointItem(20, 20);

        workflow.ReverseWaypoints();

        host.Mission.Items.Select(item => item.Id).Should().Equal(second.Id, first.Id);
        host.MissionChanges.Should().Equal("Waypoints reversed.");
    }

    [Fact]
    public void ReverseWaypoints_WithOneItem_Reports()
    {
        host.AddWaypointItem(10, 10);

        workflow.ReverseWaypoints();

        host.LastStatus.Should().Be("Nothing to reverse.");
    }

    [Fact]
    public void LoiterForever_UsesTheEditorLoiterRadius()
    {
        host.ContextPosition = new GeoPosition(56, 10);

        workflow.LoiterForever();

        var loiter = host.Mission.Items.Single().Should().BeOfType<LoiterMissionItem>().Subject;
        loiter.RadiusMeters.Should().Be(45);
    }

    [Fact]
    public void AddReturnToLaunch_AppendsAnRtlItem()
    {
        workflow.AddReturnToLaunch();

        host.Mission.Items.Single().Should().BeOfType<ReturnToLaunchMissionItem>();
        host.MissionChanges.Should().Equal("RTL added.");
    }

    [Fact]
    public void AddLand_WithoutPosition_Reports()
    {
        workflow.AddLand();

        host.Mission.Items.Should().BeEmpty();
        host.LastStatus.Should().Be("No map position selected.");
    }

    [Fact]
    public async Task AddTakeoff_UsesThePromptedAltitude()
    {
        dialogs.PromptAsync("Takeoff", Arg.Any<string>(), Arg.Any<double>(), Arg.Any<double?>(), Arg.Any<double?>(),
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((double?)35);

        await workflow.AddTakeoffAsync();

        var takeoff = host.Mission.Items.Single().Should().BeOfType<TakeoffMissionItem>().Subject;
        takeoff.Altitude.Meters.Should().Be(35);
    }

    [Fact]
    public async Task AddTakeoff_PromptCancelled_ChangesNothing()
    {
        dialogs.PromptAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<double>(), Arg.Any<double?>(), Arg.Any<double?>(),
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((double?)null);

        await workflow.AddTakeoffAsync();

        host.Mission.Items.Should().BeEmpty();
        host.MissionChanges.Should().BeEmpty();
    }

    [Fact]
    public async Task ModifyAltitude_RewritesPositionedItemsAndTheDefault()
    {
        host.AddWaypointItem(10, 10, 20);
        host.Mission.Add(new ReturnToLaunchMissionItem(MissionItemId.New(), 0));
        dialogs.PromptAsync("Modify Alt", Arg.Any<string>(), Arg.Any<double>(), Arg.Any<double?>(), Arg.Any<double?>(),
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((double?)250);

        await workflow.ModifyAltitudeAsync();

        host.DefaultAltitudeMeters.Should().Be(250);
        MissionItemGeometry.AltitudeOf(host.Mission.Items[0])!.Value.Meters.Should().Be(250);
        host.Mission.Items[1].Should().BeOfType<ReturnToLaunchMissionItem>();
        host.MissionChanges.Should().Equal("Altitude set to 250 m.");
    }

    [Fact]
    public async Task JumpToStart_AddsAJumpAfterAValidRepeatCount()
    {
        host.AddWaypointItem(10, 10);
        dialogs.PromptAsync("DO_JUMP", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<CancellationToken>()).Returns("2");

        await workflow.JumpToStartAsync(CancellationToken.None);

        host.Mission.Items.Last().Should().BeOfType<JumpMissionItem>();
        host.MissionChanges.Should().Equal("DO_JUMP to mission start added.");
    }

    [Fact]
    public async Task JumpToStart_InfiniteRepeatDeclined_AddsNothing()
    {
        host.AddWaypointItem(10, 10);
        dialogs.PromptAsync("DO_JUMP", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<CancellationToken>()).Returns("-1");
        confirmations.ConfirmAsync("Infinite DO_JUMP", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);

        await workflow.JumpToStartAsync(CancellationToken.None);

        host.Mission.Items.Should().HaveCount(1);
        host.MissionChanges.Should().BeEmpty();
    }

    [Fact]
    public async Task JumpToStart_InvalidRepeatCount_Reports()
    {
        dialogs.PromptAsync("DO_JUMP", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<CancellationToken>()).Returns("-5");

        await workflow.JumpToStartAsync(CancellationToken.None);

        host.LastStatus.Should().Be("Repeat count must be -1, zero, or positive.");
    }
}
