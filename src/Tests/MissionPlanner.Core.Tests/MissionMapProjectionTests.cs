using FluentAssertions;
using MissionPlanner.App.Views.Missions;
using MissionPlanner.Core.Missions.Models;
using MissionPlanner.Core.Vehicles.Models;

namespace MissionPlanner.Core.Tests;

/// <summary>Verifies UI-neutral mission map projection and geographic calculations.</summary>
public sealed class MissionMapProjectionTests
{
    /// <summary>Verifies home and positioned mission items produce ordered map content.</summary>
    [Fact]
    public void CreateBuildsOrderedMarkersRouteAndBounds()
    {
        var home = new GeoPosition(55.0, 10.0);
        var waypoint = new GeoPosition(55.1, 10.2);
        var mission = new Mission(MissionId.New(), "Map test");
        mission.Add(new WaypointMissionItem(
            MissionItemId.New(),
            0,
            waypoint,
            new MissionAltitude(100, MissionAltitudeReference.Home),
            TimeSpan.Zero));

        var snapshot = MissionMapProjection.Create(mission, home);

        snapshot.Markers.Select(marker => marker.Kind).Should().Equal(
            MissionMapMarkerKind.Home,
            MissionMapMarkerKind.MissionItem);
        snapshot.Route.Should().Equal(home, waypoint);
        snapshot.Bounds.Should().NotBeNull();
        snapshot.Bounds!.Value.South.Should().BeLessThan(home.LatitudeDegrees);
        snapshot.Bounds.Value.East.Should().BeGreaterThan(waypoint.LongitudeDegrees);
    }

    /// <summary>Verifies invalid input is ignored and an empty set has no bounds.</summary>
    [Fact]
    public void CalculateBoundsIgnoresInvalidPositions()
    {
        var bounds = GeographicCalculations.CalculateBounds(
        [
            new GeoPosition(double.NaN, 10),
            new GeoPosition(55, 10)
        ]);

        bounds.Should().NotBeNull();
        bounds!.Value.Center.LatitudeDegrees.Should().BeApproximately(55, 0.000001);
        GeographicCalculations.CalculateBounds([]).Should().BeNull();
    }

    /// <summary>Verifies structurally identical snapshots do not require another redraw.</summary>
    [Fact]
    public void ContentEqualsUsesProjectedValues()
    {
        var mission = new Mission(MissionId.New(), "Map test");
        mission.Add(new WaypointMissionItem(
            MissionItemId.New(),
            0,
            new GeoPosition(55, 10),
            new MissionAltitude(100, MissionAltitudeReference.Home),
            TimeSpan.Zero));

        var first = MissionMapProjection.Create(mission, null);
        var second = MissionMapProjection.Create(mission, null);

        first.ContentEquals(second).Should().BeTrue();
    }
}
