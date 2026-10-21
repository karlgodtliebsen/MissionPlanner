using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using MissionPlanner.App.Views.Missions;
using MissionPlanner.App.Views.Missions.Workflows;
using MissionPlanner.Core.Missions;
using MissionPlanner.Core.Missions.Files;
using MissionPlanner.Core.Missions.Models;
using NSubstitute;

namespace MissionPlanner.AvaloniaUI.Tests;

/// <summary>Verifies the mission list rows, their pending edits, and the file workflow that feeds them.</summary>
public sealed class MissionRowEditorTests
{
    private readonly FakeMissionEditorHost host = new();
    private readonly MissionRowEditor editor = new(new MissionRowProjector(new MissionProtocolMapper()), NullLogger<MissionRowEditor>.Instance);
    private readonly List<string> summaries = [];
    private int dirtyNotifications;

    public MissionRowEditorTests()
    {
        editor.Attach(host);
        editor.SummaryChanged += summaries.Add;
        editor.DirtyStateChanged += (_, _) => dirtyNotifications++;
    }

    [Fact]
    public void Rebuild_CreatesOneRowPerItemAndPublishesTheSummary()
    {
        host.AddWaypointItem(56, 10);
        host.AddWaypointItem(56.001, 10);

        editor.Rebuild();

        editor.Rows.Select(row => row.Number).Should().Equal(1, 2);
        summaries.Should().ContainSingle().Which.Should().StartWith("2 items");
        dirtyNotifications.Should().Be(1);
        editor.HasDirtyRows().Should().BeFalse();
    }

    [Fact]
    public void Rebuild_DiscardsPendingEdits()
    {
        host.AddWaypointItem(56, 10);
        editor.Rebuild();
        editor.Rows[0].Altitude = 500;
        editor.HasDirtyRows().Should().BeTrue();

        editor.Rebuild();

        editor.HasDirtyRows().Should().BeFalse();
        editor.Rows[0].Altitude.Should().Be(50);
    }

    [Fact]
    public void EditingAValue_MarksTheRowDirtyOnceAndReportsTheState()
    {
        host.AddWaypointItem(56, 10);
        editor.Rebuild();
        summaries.Clear();

        editor.Rows[0].Altitude = 120;
        editor.Rows[0].Param1 = "5";

        editor.DirtyRows.Should().ContainSingle();
        summaries.Should().OnlyContain(summary => summary == "EditStateChanged IsDirty: True");
    }

    [Fact]
    public void ApplyAll_WritesTheEditedValuesBackToTheMission()
    {
        var item = host.AddWaypointItem(56, 10);
        editor.Rebuild();
        editor.Rows[0].Altitude = 120;

        editor.ApplyAll();

        MissionItemGeometry.AltitudeOf(host.Mission.Items.Single())!.Value.Meters.Should().BeApproximately(120, 1e-3);
        host.Mission.Items.Should().ContainSingle();
        host.MissionChanges.Should().Equal("Item 1 updated.");
    }

    [Fact]
    public void ApplyAll_WithoutPendingEdits_DoesNothing()
    {
        host.AddWaypointItem(56, 10);
        editor.Rebuild();
        dirtyNotifications = 0;

        editor.ApplyAll();

        host.MissionChanges.Should().BeEmpty();
        dirtyNotifications.Should().Be(0);
    }

    [Fact]
    public void ChangingTheCommandSelection_AppliesImmediately()
    {
        host.AddWaypointItem(56, 10);
        editor.Rebuild();

        editor.Rows[0].SelectedCommand = "LOITER_UNLIM";

        host.Mission.Items.Single().Should().BeOfType<LoiterMissionItem>();
        host.MissionChanges.Should().Equal("Item 1 updated.");
    }

    [Fact]
    public void ApplyingARowForAnItemThatNoLongerExists_IsIgnored()
    {
        var item = host.AddWaypointItem(56, 10);
        editor.Rebuild();
        editor.Rows[0].Altitude = 99;
        host.Mission.Remove(item.Id);

        editor.ApplyAll();

        host.Mission.Items.Should().BeEmpty();
        host.MissionChanges.Should().BeEmpty();
    }

    [Fact]
    public void ClearDirty_ForgetsPendingEditsWithoutRebuildingRows()
    {
        host.AddWaypointItem(56, 10);
        editor.Rebuild();
        var row = editor.Rows[0];
        row.Altitude = 120;

        editor.ClearDirty();

        editor.HasDirtyRows().Should().BeFalse();
        editor.Rows[0].Should().BeSameAs(row);
    }

    [Fact]
    public void Dispose_DetachesRowCallbacks()
    {
        host.AddWaypointItem(56, 10);
        editor.Rebuild();
        var row = editor.Rows[0];

        editor.Dispose();
        row.Altitude = 77;

        editor.HasDirtyRows().Should().BeFalse();
    }
}

/// <summary>Verifies saving and loading mission files through the editor.</summary>
public sealed class MissionFileWorkflowTests
{
    private readonly FakeMissionEditorHost host = new();
    private readonly IMissionFileInteraction files = Substitute.For<IMissionFileInteraction>();
    private readonly MissionFileWorkflow workflow;

    public MissionFileWorkflowTests()
    {
        workflow = new MissionFileWorkflow(files, NullLogger<MissionFileWorkflow>.Instance);
        workflow.Attach(host);
    }

    private static WaypointMissionItem Waypoint(double latitude)
    {
        return new WaypointMissionItem(MissionItemId.New(), 0, new GeoPosition(latitude, 10),
            new MissionAltitude(50, MissionAltitudeReference.Home), TimeSpan.Zero, 30);
    }

    [Fact]
    public async Task Save_ShowsTheMessageAndDiscardsPendingEditsWhenTheSaveRan()
    {
        files.SaveMissionAsync(host.Mission, null, Arg.Any<CancellationToken>()).Returns(new MissionSaveResult("Mission saved to x.", true));

        await workflow.SaveWpFileAsync(CancellationToken.None);

        host.LastStatus.Should().Be("Mission saved to x.");
        host.ClearedRowEdits.Should().Be(1);
    }

    [Fact]
    public async Task Save_CancelledSilently_ChangesNothing()
    {
        files.SaveMissionAsync(Arg.Any<Mission>(), Arg.Any<GeoPosition?>(), Arg.Any<CancellationToken>()).Returns(new MissionSaveResult(null, false));

        await workflow.SaveWpFileAsync(CancellationToken.None);

        host.Statuses.Should().BeEmpty();
        host.ClearedRowEdits.Should().Be(0);
    }

    [Fact]
    public async Task Load_ReplacesTheMissionAndHomeAndFitsTheMap()
    {
        host.AddWaypointItem(1, 1);
        var content = new MissionFileContent([Waypoint(56), Waypoint(57)], new GeoPosition(55, 9), 0, "Loaded plan");
        files.LoadMissionAsync(Arg.Any<CancellationToken>()).Returns(new MissionLoadResult(content, "plan.waypoints", null));

        await workflow.LoadWpFileAsync(CancellationToken.None);

        host.Mission.Name.Should().Be("Loaded plan");
        host.Mission.Items.Should().HaveCount(2);
        host.HomePosition.Should().Be(new GeoPosition(55, 9));
        host.FitRequests.Should().Be(1);
        host.MissionChanges.Should().Equal("Loaded 2 items from plan.waypoints.");
    }

    [Fact]
    public async Task Load_WithoutAMissionName_UsesTheFileName()
    {
        var content = new MissionFileContent([Waypoint(56)], null, 0);
        files.LoadMissionAsync(Arg.Any<CancellationToken>()).Returns(new MissionLoadResult(content, "survey.waypoints", null));

        await workflow.LoadWpFileAsync(CancellationToken.None);

        host.Mission.Name.Should().Be("survey");
        host.HomePosition.Should().BeNull();
    }

    [Fact]
    public async Task LoadAndAppend_KeepsExistingItemsAndHome()
    {
        host.AddWaypointItem(1, 1);
        host.HomePosition = new GeoPosition(1, 1);
        var content = new MissionFileContent([Waypoint(56)], new GeoPosition(55, 9), 1);
        files.LoadMissionAsync(Arg.Any<CancellationToken>()).Returns(new MissionLoadResult(content, "more.waypoints", null));

        await workflow.LoadAndAppendAsync(CancellationToken.None);

        host.Mission.Items.Should().HaveCount(2);
        host.HomePosition.Should().Be(new GeoPosition(1, 1));
        host.MissionChanges.Should().Equal("Loaded 1 items from more.waypoints; skipped 1 unsupported.");
    }

    [Fact]
    public async Task Load_Failure_ReportsAndKeepsTheMission()
    {
        host.AddWaypointItem(1, 1);
        files.LoadMissionAsync(Arg.Any<CancellationToken>()).Returns(new MissionLoadResult(null, string.Empty, "Load failed: bad file"));

        await workflow.LoadWpFileAsync(CancellationToken.None);

        host.LastStatus.Should().Be("Load failed: bad file");
        host.Mission.Items.Should().HaveCount(1);
        host.FitRequests.Should().Be(0);
    }

    [Fact]
    public async Task Load_Cancelled_IsSilent()
    {
        files.LoadMissionAsync(Arg.Any<CancellationToken>()).Returns(new MissionLoadResult(null, string.Empty, null));

        await workflow.LoadWpFileAsync(CancellationToken.None);

        host.Statuses.Should().BeEmpty();
    }
}
