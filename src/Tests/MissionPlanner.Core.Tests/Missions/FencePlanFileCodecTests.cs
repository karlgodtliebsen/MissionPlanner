using FluentAssertions;
using MissionPlanner.Core.ConfigTuning.Fences;
using MissionPlanner.Core.Missions.Models;

namespace MissionPlanner.Core.Tests;

public sealed class FencePlanFileCodecTests
{
    [Fact]
    public void RoundTrip_PreservesCompleteFencePlan()
    {
        var codec = new FencePlanFileCodec(new FenceGeometryValidator());
        var plan = new FencePlan(new GeoPosition(56, 10),
        [
            FenceArea.Polygon(FenceAreaKind.PolygonInclusion,
                [new(56, 10), new(56, 10.01), new(56.01, 10.01)], true),
            FenceArea.Circle(FenceAreaKind.CircleExclusion, new(56.005, 10.005), 125)
        ]);
        var restored = codec.Deserialize(codec.Serialize(plan));
        restored.ReturnPoint.Should().Be(plan.ReturnPoint);
        restored.Areas.Should().BeEquivalentTo(plan.Areas);
    }

    [Fact]
    public void Deserialize_RejectsInvalidGeometry()
    {
        var codec = new FencePlanFileCodec(new FenceGeometryValidator());
        var json = codec.Serialize(new FencePlan(null, [FenceArea.Circle(FenceAreaKind.CircleInclusion, new(56, 10), 0)]));
        codec.Invoking(candidate => candidate.Deserialize(json)).Should().Throw<InvalidDataException>();
    }
}
