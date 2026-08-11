using FluentAssertions;
using MissionPlanner.Core.Missions.Models;
using MissionPlanner.Core.Missions.Planning;
using MissionPlanner.Maps.Coordinates;

namespace MissionPlanner.Core.Tests;

public sealed class GeodeticCoordinateConverterTests
{
    [Theory]
    [InlineData(56.1629, 10.2039)]
    [InlineData(-33.8688, 151.2093)]
    [InlineData(0.1, 5.999)]
    public void UtmRoundTrip_IsAccurate(double latitude, double longitude)
    {
        var converter = new GeodeticCoordinateConverter();
        var restored = converter.ToGeographic(converter.ToUtm(new(latitude, longitude)));
        restored.Latitude.Should().BeApproximately(latitude, .00001);
        restored.Longitude.Should().BeApproximately(longitude, .00001);
    }

    [Fact]
    public void Parser_ValidatesZoneHemisphereAndRanges()
    {
        var converter = new GeodeticCoordinateConverter();
        converter.ParseUtm("32N 500000 6170000").Should().Be(new UtmCoordinate(32, 'N', 500000, 6170000));
        converter.Invoking(value => value.ParseUtm("61N 500000 6170000")).Should().Throw<ArgumentOutOfRangeException>();
        converter.Invoking(value => value.ParseUtm("32X 500000 6170000")).Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void TrackerHome_IsLocalStateOnlyAndRaisesChange()
    {
        var service = new TrackerHomeService(); var changed = 0; service.Changed += (_, _) => changed++;
        service.Set(new GeoPosition(56,10), 20, DateTimeOffset.UnixEpoch, "test");
        service.Snapshot!.Position.Should().Be(new GeoPosition(56,10)); changed.Should().Be(1);
    }
}
