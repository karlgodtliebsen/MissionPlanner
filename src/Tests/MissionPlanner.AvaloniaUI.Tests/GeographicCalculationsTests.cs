using FluentAssertions;
using MissionPlanner.App.Views.Missions;
using MissionPlanner.Core.Missions.Models;

namespace MissionPlanner.AvaloniaUI.Tests;

/// <summary>Verifies the distance and bearing helpers shared by the mission editor.</summary>
public sealed class GeographicCalculationsTests
{
    [Fact]
    public void CalculateDistanceAndBearing_DueEast_IsNinetyDegrees()
    {
        var (distance, bearing) = GeographicCalculations.CalculateDistanceAndBearing(new GeoPosition(0, 0), new GeoPosition(0, 1));

        distance.Should().BeApproximately(111195, 50);
        bearing.Should().BeApproximately(90, 0.001);
    }

    [Fact]
    public void CalculateDistanceAndBearing_DueSouth_Is180Degrees()
    {
        var (_, bearing) = GeographicCalculations.CalculateDistanceAndBearing(new GeoPosition(10, 20), new GeoPosition(9, 20));

        bearing.Should().BeApproximately(180, 0.001);
    }

    [Theory]
    [InlineData(1, 0, 0)]
    [InlineData(0, 1, 90)]
    [InlineData(-1, 0, 180)]
    [InlineData(0, -1, 270)]
    public void BearingDegrees_CardinalDirections_AreNormalizedToZeroTo360(double deltaLat, double deltaLon, double expected)
    {
        GeographicCalculations.BearingDegrees(new GeoPosition(0, 0), new GeoPosition(deltaLat, deltaLon)).Should().BeApproximately(expected, 0.001);
    }

    [Fact]
    public void CalculateBounds_NoValidPositions_ReturnsNull()
    {
        GeographicCalculations.CalculateBounds([]).Should().BeNull();
    }
}
