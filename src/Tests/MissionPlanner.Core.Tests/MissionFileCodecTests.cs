using FluentAssertions;
using MissionPlanner.Core.Missions;
using MissionPlanner.Core.Missions.Files;
using MissionPlanner.Core.Missions.Models;

namespace MissionPlanner.Core.Tests;

/// <summary>
/// Tests for mission file serialization: QGC WPL (.waypoints/.txt) and JSON (.mission).
/// </summary>
public class MissionFileCodecTests
{
    private readonly MissionFileCodec codec = new(new MissionProtocolMapper());

    private static Mission BuildMission()
    {
        var mission = new Mission(MissionId.New(), "Test Mission");
        mission.Add(new TakeoffMissionItem(MissionItemId.New(), 0, null, new MissionAltitude(40, MissionAltitudeReference.Home)));
        mission.Add(new WaypointMissionItem(MissionItemId.New(), 0, new GeoPosition(55.1234567, 10.7654321),
            new MissionAltitude(100, MissionAltitudeReference.Home), TimeSpan.FromSeconds(5)));
        mission.Add(new LoiterMissionItem(MissionItemId.New(), 0, new GeoPosition(55.2, 10.8),
            new MissionAltitude(80, MissionAltitudeReference.Home), Time: TimeSpan.FromSeconds(30)));
        mission.Add(new ReturnToLaunchMissionItem(MissionItemId.New(), 0));
        return mission;
    }

    /// <summary>
    /// A mission written as QGC WPL parses back with identical items and home.
    /// </summary>
    [Fact]
    public void Should_Round_Trip_QgcWpl()
    {
        var mission = BuildMission();
        var home = new GeoPosition(55.0, 10.0);

        var content = codec.Build(mission, home, MissionFileFormat.QgcWpl);
        content.Should().StartWith("QGC WPL 110");

        var parsed = codec.Parse(content);

        parsed.SkippedItems.Should().Be(0);
        parsed.Home.Should().Be(home);
        parsed.Items.Should().HaveCount(mission.Items.Count);
        parsed.Items.Should().BeEquivalentTo(mission.Items, options => options
            .Excluding(x => x.Id)
            .RespectingRuntimeTypes());
    }

    /// <summary>
    /// A mission written as JSON (.mission) parses back with identical items, home and name.
    /// </summary>
    [Fact]
    public void Should_Round_Trip_MissionJson()
    {
        var mission = BuildMission();
        var home = new GeoPosition(55.0, 10.0);

        var content = codec.Build(mission, home, MissionFileFormat.MissionJson);
        content.TrimStart().Should().StartWith("{");
        content.Should().Contain("\"version\"");

        var parsed = codec.Parse(content);

        parsed.SkippedItems.Should().Be(0);
        parsed.Home.Should().Be(home);
        parsed.Name.Should().Be("Test Mission");
        parsed.Items.Should().HaveCount(mission.Items.Count);
        parsed.Items.Should().BeEquivalentTo(mission.Items, options => options
            .Excluding(x => x.Id)
            .RespectingRuntimeTypes());
    }

    /// <summary>
    /// Content that is neither WPL nor JSON is rejected.
    /// </summary>
    [Fact]
    public void Should_Reject_Unknown_Format()
    {
        var act = () => codec.Parse("this is not a mission file");
        act.Should().Throw<InvalidDataException>();
    }

    /// <summary>
    /// JSON items with unsupported commands are skipped, not fatal.
    /// </summary>
    [Fact]
    public void Should_Skip_Unsupported_Commands_In_Json()
    {
        const string json = """
        {
          "version": 1,
          "name": "Partial",
          "items": [
            { "sequence": 0, "command": 16, "frame": 3, "autoContinue": true,
              "param1": 0, "param2": 0, "param3": 0, "param4": 0,
              "latitude": 55.0, "longitude": 10.0, "altitudeMeters": 50 },
            { "sequence": 1, "command": 177, "frame": 3, "autoContinue": true,
              "param1": 0, "param2": 0, "param3": 0, "param4": 0,
              "latitude": 0, "longitude": 0, "altitudeMeters": 0 }
          ]
        }
        """;

        var parsed = codec.Parse(json);

        parsed.Items.Should().HaveCount(1);
        parsed.SkippedItems.Should().Be(1);
    }
}
