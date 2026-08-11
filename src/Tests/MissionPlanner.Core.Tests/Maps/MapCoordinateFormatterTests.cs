using MissionPlanner.Maps.Coordinates;

namespace MissionPlanner.Core.Tests.Maps;

/// <summary>Verifies map status coordinate formatting.</summary>
public sealed class MapCoordinateFormatterTests
{
    /// <summary>Verifies geographic formatting retains status-bar precision.</summary>
    [Fact]
    public void Format_Geo_UsesLatitudeAndLongitude()
    {
        var result = MapCoordinateFormatter.Format("GEO", 56.1863528, 10.2143282);

        Assert.Equal("Lat: 56.1863528  Lon: 10.2143282", result);
    }

    /// <summary>Verifies the Eiffel Tower against a published UTM reference.</summary>
    [Fact]
    public void Format_Utm_UsesWgs84ZoneAndMetres()
    {
        var result = MapCoordinateFormatter.Format("UTM", 48.8582, 2.2945);

        Assert.Equal("UTM: 31N 448252 E 5411933 N", result);
    }

    /// <summary>Verifies the Eiffel Tower against its five-digit MGRS reference.</summary>
    [Fact]
    public void Format_Mgrs_UsesGridSquareAndFiveDigitPrecision()
    {
        var result = MapCoordinateFormatter.Format("MGRS", 48.8582, 2.2945);

        Assert.Equal("MGRS: 31U DQ 48251 11932", result);
    }

    /// <summary>Verifies projected formats communicate their polar coverage limit.</summary>
    [Theory]
    [InlineData("UTM")]
    [InlineData("MGRS")]
    public void Format_ProjectedOutsideCoverage_ReportsUnavailable(string style)
    {
        Assert.Equal($"{style}: outside coverage", MapCoordinateFormatter.Format(style, 85, 0));
    }
}
