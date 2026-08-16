using FluentAssertions;
using MissionPlanner.App.Views.Common;

namespace MissionPlanner.Core.Tests;

/// <summary>Verifies stable RC PWM meter geometry.</summary>
public sealed class RadioChannelMeterGeometryTests
{
    /// <summary>Verifies the standard display domain maps to the complete rail.</summary>
    [Theory]
    [InlineData(800, 0)]
    [InlineData(1500, 50)]
    [InlineData(2200, 100)]
    [InlineData(500, 0)]
    [InlineData(2500, 100)]
    public void PositionMapsAndClampsDisplayDomain(int pwm, float expected)
    {
        RadioChannelMeterGeometry.Position(pwm, 800, 2200, 0, 100).Should().BeApproximately(expected, 0.001f);
    }

    /// <summary>Verifies live values outside configured endpoints remain spatially distinct.</summary>
    [Fact]
    public void LiveValueBelowConfiguredMinimumRemainsVisibleToItsLeft()
    {
        var current = RadioChannelMeterGeometry.Position(880, 800, 2200, 0, 140);
        var configuredMinimum = RadioChannelMeterGeometry.Position(1100, 800, 2200, 0, 140);

        current.Should().BeLessThan(configuredMinimum);
    }

    /// <summary>Verifies configured, trim, and captured values share the stable visual mapping.</summary>
    [Fact]
    public void MarkerPositionsUseStableDisplayDomain()
    {
        RadioChannelMeterGeometry.Position(1100, 800, 2200, 10, 140).Should().BeApproximately(40, 0.001f);
        RadioChannelMeterGeometry.Position(1500, 800, 2200, 10, 140).Should().BeApproximately(80, 0.001f);
        RadioChannelMeterGeometry.Position(1900, 800, 2200, 10, 140).Should().BeApproximately(120, 0.001f);
        RadioChannelMeterGeometry.Position(900, 800, 2200, 10, 140).Should().BeApproximately(20, 0.001f);
        RadioChannelMeterGeometry.Position(2100, 800, 2200, 10, 140).Should().BeApproximately(140, 0.001f);
    }

    /// <summary>Verifies dead-zone boundaries are centered on actual trim.</summary>
    [Fact]
    public void DeadZoneUsesConfiguredTrim()
    {
        var positions = RadioChannelMeterGeometry.DeadZone(1520, 30, 800, 2200, 0, 140);

        positions.Left.Should().BeApproximately(69, 0.001f);
        positions.Right.Should().BeApproximately(75, 0.001f);
    }
}
