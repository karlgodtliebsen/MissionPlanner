using FluentAssertions;
using MissionPlanner.Core.Missions;
using MissionPlanner.Core.Missions.Models;

namespace MissionPlanner.Core.Tests;

/// <summary>
/// Tests that mission items survive a round trip through the MAVLink protocol representation.
/// </summary>
public class MissionProtocolMapperTests
{
    private readonly MissionProtocolMapper mapper = new();

    /// <summary>
    /// Mission items covering every supported command and the null/absent parameter conventions.
    /// </summary>
    public static TheoryData<MissionItem> RoundTripItems => new()
    {
        new WaypointMissionItem(MissionItemId.New(), 1, new GeoPosition(55.1234567, 10.7654321),
            new MissionAltitude(100, MissionAltitudeReference.Home), TimeSpan.FromSeconds(5),
            2, 1, 90),
        new WaypointMissionItem(MissionItemId.New(), 2, new GeoPosition(-33.9, 151.2),
            new MissionAltitude(50, MissionAltitudeReference.MeanSeaLevel), TimeSpan.Zero),
        new LoiterMissionItem(MissionItemId.New(), 3, new GeoPosition(55.5, 10.5),
            new MissionAltitude(80, MissionAltitudeReference.Home), RadiusMeters: 25),
        new LoiterMissionItem(MissionItemId.New(), 4, new GeoPosition(55.5, 10.5),
            new MissionAltitude(80, MissionAltitudeReference.Home), TimeSpan.FromSeconds(30)),
        new LoiterMissionItem(MissionItemId.New(), 5, new GeoPosition(55.5, 10.5),
            new MissionAltitude(80, MissionAltitudeReference.Terrain), Turns: 3),
        new ReturnToLaunchMissionItem(MissionItemId.New(), 6),
        new LandMissionItem(MissionItemId.New(), 7, new GeoPosition(55.6, 10.6),
            new MissionAltitude(0, MissionAltitudeReference.Home), 15, 180),
        new TakeoffMissionItem(MissionItemId.New(), 8, null,
            new MissionAltitude(40, MissionAltitudeReference.Home), 10, 45),
        new TakeoffMissionItem(MissionItemId.New(), 9, new GeoPosition(55.7, 10.7),
            new MissionAltitude(40, MissionAltitudeReference.Home)),
        new ChangeSpeedMissionItem(MissionItemId.New(), 10, MissionSpeedType.GroundSpeed, 12.5),
        new ChangeSpeedMissionItem(MissionItemId.New(), 11, MissionSpeedType.Airspeed, 18, 75)
    };

    /// <summary>
    /// Round trips a mission item via ToProtocol and FromProtocol and verifies everything except the
    /// (regenerated) identifier is preserved.
    /// </summary>
    /// <param name="item">The mission item to round trip.</param>
    [Theory]
    [MemberData(nameof(RoundTripItems))]
    public void Should_Round_Trip_Mission_Item_Through_Protocol(MissionItem item)
    {
        var protocol = mapper.ToProtocol(item, MissionPlanType.FlightMission);
        var result = mapper.FromProtocol(protocol);

        result.Should().BeOfType(item.GetType());
        result.Should().BeEquivalentTo(item, options => options.Excluding(x => x.Id));
        result.Command.Should().Be(item.Command);
        result.Frame.Should().Be(item.Frame);
    }

    /// <summary>
    /// Verifies that an unsupported protocol command is rejected instead of being silently mistranslated.
    /// </summary>
    [Fact]
    public void Should_Reject_Unsupported_Command()
    {
        var waypoint = new WaypointMissionItem(MissionItemId.New(), 1, new GeoPosition(55, 10),
            new MissionAltitude(100, MissionAltitudeReference.Home), TimeSpan.Zero);
        var protocol = mapper.ToProtocol(waypoint, MissionPlanType.FlightMission) with { Command = 177 };

        var act = () => mapper.FromProtocol(protocol);

        act.Should().Throw<NotSupportedException>();
    }
}
