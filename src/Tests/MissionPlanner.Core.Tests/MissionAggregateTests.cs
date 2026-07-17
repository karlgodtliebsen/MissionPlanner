using FluentAssertions;
using MissionPlanner.Core.Missions.Models;

namespace MissionPlanner.Core.Tests;

/// <summary>
/// Tests mission aggregate sequencing and argument validation.
/// </summary>
public sealed class MissionAggregateTests
{
    [Fact]
    public void Should_Resequence_Items_After_Move()
    {
        var mission = CreateMissionWithThreeWaypoints();
        var first = mission.Items[0];

        mission.Move(first.Id, 2);

        mission.Items.Select(x => x.Sequence).Should().Equal((ushort)0, (ushort)1, (ushort)2);
        mission.Items[2].Id.Should().Be(first.Id);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(3)]
    public void Should_Reject_Invalid_Move_Destination(int destination)
    {
        var mission = CreateMissionWithThreeWaypoints();

        var act = () => mission.Move(mission.Items[0].Id, destination);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(4)]
    public void Should_Reject_Invalid_Insert_Index(int index)
    {
        var mission = CreateMissionWithThreeWaypoints();
        var item = CreateWaypoint(0, 56, 11);

        var act = () => mission.Insert(index, item);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    private static Mission CreateMissionWithThreeWaypoints()
    {
        var mission = new Mission(MissionId.New(), "Aggregate Test");
        mission.Add(CreateWaypoint(0, 55, 10));
        mission.Add(CreateWaypoint(0, 55.1, 10.1));
        mission.Add(CreateWaypoint(0, 55.2, 10.2));
        return mission;
    }

    private static WaypointMissionItem CreateWaypoint(ushort sequence, double latitude, double longitude)
    {
        return new WaypointMissionItem(
            MissionItemId.New(),
            sequence,
            new GeoPosition(latitude, longitude),
            new MissionAltitude(100, MissionAltitudeReference.Home),
            TimeSpan.Zero);
    }
}
