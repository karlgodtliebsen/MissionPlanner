using FluentAssertions;
using MissionPlanner.App.Views.Missions;
using MissionPlanner.Core.Missions.Models;

namespace MissionPlanner.Core.Tests;

public sealed class MissionMapMeasurementTests
{
    [Fact]
    public void CalculateDistanceAndBearing_UsesGreatCircleGeometry()
    {
        var result = MissionMapViewModel.CalculateDistanceAndBearing(new GeoPosition(56, 10), new GeoPosition(56, 10.01));
        result.Distance.Should().BeApproximately(621.8, 2);
        result.Bearing.Should().BeApproximately(90, .1);
    }
}
