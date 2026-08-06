namespace MissionPlanner.Core.Vehicles;

/// <summary>
/// Geographic distance helpers for telemetry display.
/// </summary>
public static class GeoMath
{
    private const double MetersPerDegree = 111319.5;
    private const double DegreesToRadians = Math.PI / 180.0;

    /// <summary>
    /// Approximate ground distance in meters between two coordinates using an equirectangular
    /// projection (longitude scaled by the cosine of the latitude). Matches the calculation the
    /// classic MissionPlanner uses for its distance readouts; accurate for GCS-range distances.
    /// </summary>
    /// <param name="latitudeA">Latitude of the first point in degrees.</param>
    /// <param name="longitudeA">Longitude of the first point in degrees.</param>
    /// <param name="latitudeB">Latitude of the second point in degrees.</param>
    /// <param name="longitudeB">Longitude of the second point in degrees.</param>
    /// <returns>The approximate distance in meters.</returns>
    public static double ApproximateDistanceMeters(double latitudeA, double longitudeA, double latitudeB, double longitudeB)
    {
        var longitudeScale = Math.Cos(Math.Abs(latitudeA) * DegreesToRadians);
        var deltaLatitudeMeters = Math.Abs(latitudeA - latitudeB) * MetersPerDegree;
        var deltaLongitudeMeters = Math.Abs(longitudeA - longitudeB) * MetersPerDegree * longitudeScale;
        return Math.Sqrt(deltaLatitudeMeters * deltaLatitudeMeters + deltaLongitudeMeters * deltaLongitudeMeters);
    }
}
