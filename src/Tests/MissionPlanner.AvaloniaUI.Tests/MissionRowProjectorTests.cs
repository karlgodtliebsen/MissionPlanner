using FluentAssertions;
using MissionPlanner.App.Views.Missions;
using MissionPlanner.Core.Missions;
using MissionPlanner.Core.Missions.Models;

namespace MissionPlanner.AvaloniaUI.Tests;

/// <summary>Verifies row projection, derived leg values, and row-to-item conversion.</summary>
public sealed class MissionRowProjectorTests
{
    private static readonly MissionAltitude Alt100 = new(100, MissionAltitudeReference.Home);
    private readonly MissionRowProjector projector = new(new MissionProtocolMapper());

    private static Mission NewMission(params MissionItem[] items)
    {
        var mission = new Mission(MissionId.New(), "Test");
        foreach (var item in items)
        {
            mission.Add(item);
        }

        return mission;
    }

    private static WaypointMissionItem Waypoint(double latitude, double longitude, MissionAltitude altitude)
    {
        return new WaypointMissionItem(MissionItemId.New(), 0, new GeoPosition(latitude, longitude), altitude, TimeSpan.Zero, 10);
    }

    [Fact]
    public void Project_EmptyMission_ReturnsNoRowsAndZeroSummary()
    {
        var projection = projector.Project(NewMission(), null);

        projection.Rows.Should().BeEmpty();
        projection.TotalDistanceMeters.Should().Be(0);
        projection.Summary.Should().Be("0 items");
    }

    [Fact]
    public void Project_AssignsSequenceNumbersAndCatalogNames()
    {
        var mission = NewMission(Waypoint(56, 10, Alt100), new ReturnToLaunchMissionItem(MissionItemId.New(), 0));

        var rows = projector.Project(mission, null).Rows;

        rows.Select(row => row.Number).Should().Equal(1, 2);
        rows[0].SelectedCommand.Should().Be("WAYPOINT");
        rows[0].SelectedFrame.Should().Be("Relative");
        rows[1].SelectedCommand.Should().Be("RETURN_TO_LAUNCH");
        rows[0].Id.Should().Be(mission.Items[0].Id);
    }

    [Fact]
    public void Project_FirstLegStartsAtHome_AndNorthwardLegHasZeroAzimuth()
    {
        var mission = NewMission(Waypoint(56.001, 10, Alt100));

        var row = projector.Project(mission, new GeoPosition(56, 10)).Rows.Single();

        row.Distance.Should().BeApproximately(111.2, 1);
        row.Azimuth.Should().BeApproximately(0, 0.001);
        row.Gradient.Should().BeApproximately(100 / row.Distance!.Value * 100, 0.01);
    }

    [Fact]
    public void Project_WithoutHome_FirstPositionedItemHasNoLeg()
    {
        var mission = NewMission(Waypoint(56, 10, Alt100), Waypoint(56, 10.001, new MissionAltitude(150, MissionAltitudeReference.Home)));

        var rows = projector.Project(mission, null).Rows;

        rows[0].Distance.Should().BeNull();
        rows[0].Azimuth.Should().BeNull();
        rows[1].Distance.Should().BeGreaterThan(0);
        rows[1].Azimuth.Should().BeApproximately(90, 0.001);
        rows[1].Gradient.Should().BeApproximately(50 / rows[1].Distance!.Value * 100, 0.01);
    }

    [Fact]
    public void Project_ShortLeg_HasNoGradient()
    {
        var mission = NewMission(Waypoint(56, 10, Alt100), Waypoint(56, 10, new MissionAltitude(200, MissionAltitudeReference.Home)));

        var rows = projector.Project(mission, null).Rows;

        rows[1].Distance.Should().BeApproximately(0, 0.5);
        rows[1].Gradient.Should().BeNull();
    }

    [Fact]
    public void Project_UnpositionedItemDoesNotBreakTheRoute()
    {
        var mission = NewMission(
            Waypoint(56, 10, Alt100),
            new ReturnToLaunchMissionItem(MissionItemId.New(), 0),
            Waypoint(56.001, 10, Alt100));

        var projection = projector.Project(mission, null);

        projection.Rows[1].Distance.Should().BeNull();
        projection.Rows[2].Distance.Should().BeApproximately(111.2, 1);
        projection.TotalDistanceMeters.Should().BeApproximately(111.2, 1);
        projection.Summary.Should().Be("3 items • 111 m total");
    }

    [Fact]
    public void ToMissionItem_RoundTripsAnUneditedRow()
    {
        var mission = NewMission(Waypoint(56.5, 10.25, Alt100));
        var row = projector.Project(mission, null).Rows.Single();

        var item = projector.ToMissionItem(row, 0);

        item.Should().BeOfType<WaypointMissionItem>();
        MissionItemGeometry.PositionOf(item)!.Value.LatitudeDegrees.Should().BeApproximately(56.5, 1e-6);
        MissionItemGeometry.PositionOf(item)!.Value.LongitudeDegrees.Should().BeApproximately(10.25, 1e-6);
        MissionItemGeometry.AltitudeOf(item)!.Value.Meters.Should().BeApproximately(100, 1e-3);
    }

    [Fact]
    public void ToMissionItem_AppliesEditedCommandAndAltitude()
    {
        var mission = NewMission(Waypoint(56, 10, Alt100));
        var row = projector.Project(mission, null).Rows.Single();
        row.SelectedCommand = "LOITER_UNLIM";
        row.Altitude = 250;

        var item = projector.ToMissionItem(row, 0);

        item.Should().BeOfType<LoiterMissionItem>();
        MissionItemGeometry.AltitudeOf(item)!.Value.Meters.Should().BeApproximately(250, 1e-3);
    }

    [Fact]
    public void ToMissionItem_UnknownSelection_FallsBackToTheRowsOriginalCommand()
    {
        var mission = NewMission(Waypoint(56, 10, Alt100));
        var row = projector.Project(mission, null).Rows.Single();
        row.SelectedCommand = "NOT_A_COMMAND";

        projector.ToMissionItem(row, 0).Should().BeOfType<WaypointMissionItem>();
    }
}
