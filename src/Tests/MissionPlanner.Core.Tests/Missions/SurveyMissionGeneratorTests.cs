using FluentAssertions;
using MissionPlanner.Core.Missions.Models;
using MissionPlanner.Core.Missions.Planning;

namespace MissionPlanner.Core.Tests;

public sealed class SurveyMissionGeneratorTests
{
    private readonly SurveyMissionGenerator generator = new(new AutoWaypointGenerator());
    private static readonly MissionAltitude Altitude = new(80, MissionAltitudeReference.Home);

    [Fact]
    public void Grid_ClipsRectangleAndProducesDeterministicStatistics()
    {
        var polygon = new PlanningPolygon("rectangle", [new(56,10), new(56,10.002), new(56.001,10.002), new(56.001,10)]);
        var first = generator.GenerateGrid(new(polygon, 0, 30, 5, Altitude));
        var second = generator.GenerateGrid(new(polygon, 0, 30, 5, Altitude));
        first.Succeeded.Should().BeTrue();
        first.Preview.Should().Equal(second.Preview);
        first.Statistics!.LineCount.Should().BeGreaterThan(1);
        first.Statistics.DistanceMeters.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Grid_SupportsConcaveAndCrossGrid()
    {
        var polygon = new PlanningPolygon("L", [new(56,10), new(56,10.003), new(56.001,10.003), new(56.001,10.001), new(56.003,10.001), new(56.003,10)]);
        var single = generator.GenerateGrid(new(polygon, 20, 40, 0, Altitude));
        var cross = generator.GenerateGrid(new(polygon, 20, 40, 0, Altitude, true));
        cross.Statistics!.LineCount.Should().BeGreaterThan(single.Statistics!.LineCount);
        cross.Preview.Should().OnlyContain(point => point.IsValid);
    }

    [Fact]
    public void Circle_GeneratesConcentricRings()
    {
        var result = generator.GenerateCircle(new(new(56, 10), 50, 150, 50, 12, true, Altitude));
        result.Succeeded.Should().BeTrue();
        result.Items.Should().HaveCount(36);
        result.Statistics!.LineCount.Should().Be(36);
    }
}
