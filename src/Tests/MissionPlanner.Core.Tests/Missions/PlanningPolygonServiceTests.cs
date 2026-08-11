using MissionPlanner.Core.Missions.Models;
using MissionPlanner.Core.Missions.Planning;

namespace MissionPlanner.Core.Tests.MissionPlanning;

/// <summary>Verifies planning polygon geometry, persistence, and mission conversion.</summary>
public sealed class PlanningPolygonServiceTests
{
    /// <summary>Valid polygons are accepted while duplicate, short, and self-intersecting boundaries are rejected.</summary>
    [Fact]
    public void Set_ValidatesBoundary()
    {
        var service = new PlanningPolygonService();
        Assert.False(service.Set("short", [new(0, 0), new(0, 1)]).Succeeded);
        Assert.False(service.Set("duplicate", [new(0, 0), new(0, 1), new(0, 1), new(1, 0)]).Succeeded);
        Assert.False(service.Set("bow", [new(0, 0), new(1, 1), new(0, 1), new(1, 0)]).Succeeded);
        Assert.True(service.Set("square", Square()).Succeeded);
    }

    /// <summary>Mission conversion preserves positioned-item order and ignores non-positioned commands.</summary>
    [Fact]
    public void FromMission_UsesOnlyPositionedItems()
    {
        var mission = new Mission(MissionId.New(), "Mixed");
        mission.Add(Waypoint(0, 0));
        mission.Add(new ReturnToLaunchMissionItem(MissionItemId.New(), 0));
        mission.Add(Waypoint(0, .001));
        mission.Add(Waypoint(.001, .001));
        var service = new PlanningPolygonService();

        Assert.True(service.FromMission(mission).Succeeded);
        Assert.Equal(3, service.Snapshot.Polygon!.Vertices.Count);
    }

    /// <summary>Metre offsets expand and contract area without degree arithmetic.</summary>
    [Fact]
    public void PreviewOffset_UsesMetreGeometryAndDetectsCollapse()
    {
        var service = new PlanningPolygonService();
        service.Set("square", Square());
        var original = service.CalculateArea()!.SquareMeters;
        var outward = service.PreviewOffset(10);
        var inward = service.PreviewOffset(-10);

        Assert.True(outward.Succeeded);
        Assert.True(inward.Succeeded);
        service.ApplyPreview(outward.Preview!);
        Assert.True(service.CalculateArea()!.SquareMeters > original);
        var collapsed = new PlanningPolygonService();
        collapsed.Set("square", Square());
        Assert.False(collapsed.PreviewOffset(-100).Succeeded);
    }

    /// <summary>Area reports a known small equatorial square within projection tolerance.</summary>
    [Fact]
    public void CalculateArea_ReturnsExpectedUnits()
    {
        var service = new PlanningPolygonService();
        service.Set("square", Square());

        var area = service.CalculateArea()!;
        Assert.InRange(area.SquareMeters, 12_300, 12_450);
        Assert.Equal(area.SquareMeters / 10_000, area.Hectares, 8);
    }

    /// <summary>Versioned JSON round trips and malformed input is rejected without replacing state.</summary>
    [Fact]
    public void Json_RoundTripsAndRejectsMalformedContent()
    {
        var source = new PlanningPolygonService();
        source.Set("square", Square());
        var json = source.Serialize(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var target = new PlanningPolygonService();

        Assert.True(target.Deserialize(json).Succeeded);
        Assert.Equal(source.Snapshot.Polygon!.Name, target.Snapshot.Polygon!.Name);
        Assert.Equal(source.Snapshot.Polygon.Vertices, target.Snapshot.Polygon.Vertices);
        Assert.False(target.Deserialize("{broken").Succeeded);
        Assert.Equal("square", target.Snapshot.Polygon!.Name);
    }

    private static GeoPosition[] Square() => [new(0, 0), new(0, .001), new(.001, .001), new(.001, 0)];
    private static WaypointMissionItem Waypoint(double latitude, double longitude) =>
        new(MissionItemId.New(), 0, new(latitude, longitude), new(50, MissionAltitudeReference.Home), TimeSpan.Zero);
}
