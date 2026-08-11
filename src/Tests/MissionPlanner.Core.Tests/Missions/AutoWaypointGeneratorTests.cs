using FluentAssertions;
using MissionPlanner.Core.Missions.Models;
using MissionPlanner.Core.Missions.Planning;

namespace MissionPlanner.Core.Tests;

public sealed class AutoWaypointGeneratorTests
{
    private readonly AutoWaypointGenerator generator = new();
    private static readonly MissionAltitude Altitude = new(100, MissionAltitudeReference.Home);

    [Fact]
    public void Circle_UsesDirectionStartBearingAndDeterministicAltitudeProgression()
    {
        var clockwise = generator.GenerateCircle(new(new(56, 10), 100, 4, true, 90, Altitude, 130, true));
        var counter = generator.GenerateCircle(new(new(56, 10), 100, 4, false, 90, Altitude));
        clockwise.Succeeded.Should().BeTrue();
        clockwise.PreviewPositions[0].LongitudeDegrees.Should().BeGreaterThan(10);
        clockwise.PreviewPositions[1].LatitudeDegrees.Should().BeLessThan(56);
        counter.PreviewPositions[1].LatitudeDegrees.Should().BeGreaterThan(56);
        clockwise.Items.Cast<SplineWaypointMissionItem>().Select(item => item.Altitude.Meters).Should().Equal(100, 110, 120, 130);
    }

    [Fact]
    public void Circle_RemainsValidAtHighLatitude()
    {
        generator.GenerateCircle(new(new(80, 20), 1000, 12, true, 0, Altitude)).PreviewPositions.Should().OnlyContain(point => point.IsValid);
    }

    [Fact]
    public void Text_IsCrossPlatformDeterministicAndBounded()
    {
        var first = generator.GenerateText(new("HOME", new(56, 10), 20, 30, .3, Altitude));
        var second = generator.GenerateText(new("HOME", new(56, 10), 20, 30, .3, Altitude));
        first.Succeeded.Should().BeTrue();
        first.PreviewPositions.Should().Equal(second.PreviewPositions);
        generator.GenerateText(new(new string('A', 33), new(56, 10), 20, 0, .3, Altitude)).Succeeded.Should().BeFalse();
    }
}
