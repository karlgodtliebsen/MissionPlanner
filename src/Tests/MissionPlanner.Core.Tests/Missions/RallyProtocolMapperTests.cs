using FluentAssertions;
using MissionPlanner.Core.Missions.Models;
using MissionPlanner.Core.Missions.Rally;

namespace MissionPlanner.Core.Tests;

public sealed class RallyProtocolMapperTests
{
    [Theory]
    [InlineData(MissionAltitudeReference.MeanSeaLevel, 0)]
    [InlineData(MissionAltitudeReference.Home, 3)]
    [InlineData(MissionAltitudeReference.Terrain, 10)]
    public void RoundTrip_PreservesSupportedAltitudeFrames(MissionAltitudeReference reference, byte expectedFrame)
    {
        var mapper = new RallyProtocolMapper();
        var source = new RallyPlan([new(RallyPointId.New(), new(56, 10), new(120, reference))]);
        var wire = mapper.ToProtocol(source);
        wire.Single().Frame.Should().Be(expectedFrame);
        var restored = mapper.FromProtocol(wire).Points.Single();
        restored.Position.Should().Be(source.Points[0].Position);
        restored.Altitude.Should().Be(source.Points[0].Altitude);
    }

    [Fact]
    public void FileRoundTrip_PreservesRallyPlan()
    {
        var codec = new RallyPlanFileCodec();
        var plan = new RallyPlan([new(RallyPointId.New(), new(56, 10), new(75, MissionAltitudeReference.Home))]);
        codec.Deserialize(codec.Serialize(plan, DateTimeOffset.UnixEpoch)).Should().BeEquivalentTo(plan);
    }
}
