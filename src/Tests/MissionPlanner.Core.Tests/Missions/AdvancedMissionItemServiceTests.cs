using MissionPlanner.Core.Missions.Models;
using MissionPlanner.Core.Missions.Planning;

namespace MissionPlanner.Core.Tests.MissionPlanning;

/// <summary>Verifies advanced mission item creation rules.</summary>
public sealed class AdvancedMissionItemServiceTests
{
    private readonly AdvancedMissionItemService service = new();

    /// <summary>Spline and ROI commands retain their selected location and modern command type.</summary>
    [Fact]
    public void LocationCommands_AddTypedItems()
    {
        var mission = NewMission();
        var position = new GeoPosition(55, 12);
        var altitude = new MissionAltitude(80, MissionAltitudeReference.Home);

        Assert.True(service.AddSplineWaypoint(mission, position, altitude).IsEnabled);
        Assert.True(service.AddRoiLocation(mission, position, altitude).IsEnabled);
        Assert.IsType<SplineWaypointMissionItem>(mission.Items[1]);
        Assert.Equal(MissionCommand.SetRoiLocation, mission.Items[2].Command);
    }

    /// <summary>Jump-to-start targets the first executable item and preserves repeat semantics.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(-1)]
    public void AddJumpToStart_UsesFirstExecutableSequence(int repeatCount)
    {
        var mission = NewMission();

        Assert.True(service.AddJumpToStart(mission, repeatCount).IsEnabled);
        var jump = Assert.IsType<JumpMissionItem>(mission.Items[^1]);
        Assert.Equal((ushort)0, jump.TargetSequence);
        Assert.Equal(repeatCount, jump.RepeatCount);
    }

    /// <summary>Invalid targets and repeat counts are rejected without mutating the mission.</summary>
    [Fact]
    public void AddJump_RejectsInvalidInput()
    {
        var mission = NewMission();

        Assert.False(service.AddJump(mission, 3, 1).IsEnabled);
        Assert.False(service.AddJump(mission, 0, -2).IsEnabled);
        Assert.Single(mission.Items);
    }

    /// <summary>ArduPilot's practical DO_JUMP command limit is enforced.</summary>
    [Fact]
    public void AddJump_EnforcesArduPilotLimit()
    {
        var mission = NewMission();
        for (var i = 0; i < JumpMissionItem.ArduPilotCommandLimit; i++)
            Assert.True(service.AddJump(mission, 0, 1).IsEnabled);

        Assert.False(service.AddJump(mission, 0, 1).IsEnabled);
    }

    private static Mission NewMission()
    {
        var mission = new Mission(MissionId.New(), "Advanced");
        mission.Add(new WaypointMissionItem(MissionItemId.New(), 0, new GeoPosition(54, 11),
            new MissionAltitude(50, MissionAltitudeReference.Home), TimeSpan.Zero));
        return mission;
    }
}
