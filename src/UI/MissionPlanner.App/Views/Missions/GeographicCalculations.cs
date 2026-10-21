using MissionPlanner.Core.Missions.Models;

namespace MissionPlanner.App.Views.Missions;

/// <summary>Provides UI-independent geographic calculations used by mission presentation.</summary>
public static class GeographicCalculations
{
    /// <summary>Calculates padded bounds around valid geographic positions.</summary>
    /// <param name="positions">Positions to contain.</param>
    /// <param name="paddingRatio">Fractional padding applied on every side.</param>
    /// <param name="minimumPaddingDegrees">Minimum padding for a zero- or short-span axis.</param>
    /// <returns>Padded bounds, or <see langword="null"/> when no valid positions are supplied.</returns>
    public static GeographicBounds? CalculateBounds(IEnumerable<GeoPosition> positions, double paddingRatio = 0.15, double minimumPaddingDegrees = 0.0005)
    {
        ArgumentNullException.ThrowIfNull(positions);
        ArgumentOutOfRangeException.ThrowIfNegative(paddingRatio);
        ArgumentOutOfRangeException.ThrowIfNegative(minimumPaddingDegrees);

        var valid = positions.Where(position => position.IsValid).ToArray();
        if (valid.Length == 0)
        {
            return null;
        }

        var south = valid.Min(position => position.LatitudeDegrees);
        var north = valid.Max(position => position.LatitudeDegrees);
        var west = valid.Min(position => position.LongitudeDegrees);
        var east = valid.Max(position => position.LongitudeDegrees);
        var latitudePadding = Math.Max((north - south) * paddingRatio, minimumPaddingDegrees);
        var longitudePadding = Math.Max((east - west) * paddingRatio, minimumPaddingDegrees);

        return new GeographicBounds(
            Math.Max(-90, south - latitudePadding),
            Math.Max(-180, west - longitudePadding),
            Math.Min(90, north + latitudePadding),
            Math.Min(180, east + longitudePadding));
    }

    /// <summary>Calculates the flat-earth bearing in degrees (0-360) from one position to another.</summary>
    public static double BearingDegrees(GeoPosition from, GeoPosition to)
    {
        var deltaY = to.LatitudeDegrees - from.LatitudeDegrees;
        var deltaX = (to.LongitudeDegrees - from.LongitudeDegrees) * Math.Cos(from.LatitudeDegrees * Math.PI / 180.0);
        var degrees = Math.Atan2(deltaX, deltaY) * 180.0 / Math.PI;
        return degrees < 0 ? degrees + 360.0 : degrees;
    }

    /// <summary>Calculates great-circle distance and initial bearing between two WGS84 positions.</summary>
    public static (double Distance, double Bearing) CalculateDistanceAndBearing(GeoPosition first, GeoPosition second)
    {
        const double radius = 6371008.8;
        var lat1 = first.LatitudeDegrees * Math.PI / 180d;
        var lat2 = second.LatitudeDegrees * Math.PI / 180d;
        var deltaLat = lat2 - lat1;
        var deltaLon = (second.LongitudeDegrees - first.LongitudeDegrees) * Math.PI / 180d;
        var a = (Math.Sin(deltaLat / 2) * Math.Sin(deltaLat / 2)) + (Math.Cos(lat1) * Math.Cos(lat2) * Math.Sin(deltaLon / 2) * Math.Sin(deltaLon / 2));
        var distance = radius * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        var bearing = ((Math.Atan2(Math.Sin(deltaLon) * Math.Cos(lat2), (Math.Cos(lat1) * Math.Sin(lat2)) - (Math.Sin(lat1) * Math.Cos(lat2) * Math.Cos(deltaLon))) * 180d / Math.PI) + 360d) % 360d;
        return (distance, bearing);
    }
}

/// <summary>Defines an axis-aligned geographic extent.</summary>
/// <param name="South">Southern latitude.</param>
/// <param name="West">Western longitude.</param>
/// <param name="North">Northern latitude.</param>
/// <param name="East">Eastern longitude.</param>
public readonly record struct GeographicBounds(double South, double West, double North, double East)
{
    /// <summary>Gets the center of the extent.</summary>
    public GeoPosition Center => new((South + North) / 2, (West + East) / 2);
}
