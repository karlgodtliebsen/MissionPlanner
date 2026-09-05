using MissionPlanner.Core.Missions.Models;

namespace MissionPlanner.AvaloniaUI.App.Views.Missions;

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
