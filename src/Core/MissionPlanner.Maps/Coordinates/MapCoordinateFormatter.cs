using System.Globalization;

namespace MissionPlanner.Maps.Coordinates;

/// <summary>Formats WGS84 geographic coordinates for map status displays.</summary>
public static class MapCoordinateFormatter
{
    private const double SemiMajorAxis = 6378137.0;
    private const double EccentricitySquared = 0.00669437999014;
    private const double ScaleFactor = 0.9996;
    private const string LatitudeBands = "CDEFGHJKLMNPQRSTUVWXX";
    private static readonly string[] EastingLetters = ["ABCDEFGH", "JKLMNPQR", "STUVWXYZ"];
    private static readonly string[] NorthingLetters = ["ABCDEFGHJKLMNPQRSTUV", "FGHJKLMNPQRSTUVABCDE"];

    /// <summary>Formats a coordinate using GEO, UTM, or MGRS notation.</summary>
    /// <param name="style">Coordinate notation name.</param>
    /// <param name="latitude">WGS84 latitude in degrees.</param>
    /// <param name="longitude">WGS84 longitude in degrees.</param>
    /// <returns>A display-ready coordinate.</returns>
    public static string Format(string? style, double latitude, double longitude)
    {
        if (!double.IsFinite(latitude) || !double.IsFinite(longitude) || latitude is < -90 or > 90 || longitude is < -180 or > 180)
            return "Position unavailable";

        if (string.Equals(style, "GEO", StringComparison.OrdinalIgnoreCase))
            return FormattableString.Invariant($"Lat: {latitude:F7}  Lon: {longitude:F7}");

        if (latitude is < -80 or > 84)
            return $"{style}: outside coverage";

        var coordinate = ToUtm(latitude, longitude);
        return string.Equals(style, "MGRS", StringComparison.OrdinalIgnoreCase)
            ? FormatMgrs(coordinate)
            : FormattableString.Invariant($"UTM: {coordinate.Zone}{coordinate.Hemisphere} {coordinate.Easting:F0} E {coordinate.Northing:F0} N");
    }

    private static UtmCoordinate ToUtm(double latitude, double longitude)
    {
        var zone = Math.Clamp((int)Math.Floor((longitude + 180) / 6) + 1, 1, 60);
        if (latitude is >= 56 and < 64 && longitude is >= 3 and < 12)
            zone = 32;
        else if (latitude is >= 72 and < 84)
            zone = longitude switch { >= 0 and < 9 => 31, >= 9 and < 21 => 33, >= 21 and < 33 => 35, >= 33 and < 42 => 37, _ => zone };

        var latitudeRadians = DegreesToRadians(latitude);
        var longitudeRadians = DegreesToRadians(longitude);
        var centralMeridian = DegreesToRadians((zone - 1) * 6 - 177);
        var secondEccentricitySquared = EccentricitySquared / (1 - EccentricitySquared);
        var sinLatitude = Math.Sin(latitudeRadians);
        var cosLatitude = Math.Cos(latitudeRadians);
        var tanLatitude = Math.Tan(latitudeRadians);
        var radius = SemiMajorAxis / Math.Sqrt(1 - EccentricitySquared * sinLatitude * sinLatitude);
        var tangent = tanLatitude * tanLatitude;
        var curvature = secondEccentricitySquared * cosLatitude * cosLatitude;
        var arc = cosLatitude * (longitudeRadians - centralMeridian);
        var eccentricityFourth = EccentricitySquared * EccentricitySquared;
        var eccentricitySixth = eccentricityFourth * EccentricitySquared;
        var meridian = SemiMajorAxis *
            ((1 - EccentricitySquared / 4 - 3 * eccentricityFourth / 64 - 5 * eccentricitySixth / 256) * latitudeRadians
             - (3 * EccentricitySquared / 8 + 3 * eccentricityFourth / 32 + 45 * eccentricitySixth / 1024) * Math.Sin(2 * latitudeRadians)
             + (15 * eccentricityFourth / 256 + 45 * eccentricitySixth / 1024) * Math.Sin(4 * latitudeRadians)
             - 35 * eccentricitySixth / 3072 * Math.Sin(6 * latitudeRadians));
        var easting = ScaleFactor * radius * (arc + (1 - tangent + curvature) * Math.Pow(arc, 3) / 6
            + (5 - 18 * tangent + tangent * tangent + 72 * curvature - 58 * secondEccentricitySquared) * Math.Pow(arc, 5) / 120) + 500000;
        var northing = ScaleFactor * (meridian + radius * tanLatitude * (arc * arc / 2
            + (5 - tangent + 9 * curvature + 4 * curvature * curvature) * Math.Pow(arc, 4) / 24
            + (61 - 58 * tangent + tangent * tangent + 600 * curvature - 330 * secondEccentricitySquared) * Math.Pow(arc, 6) / 720));
        if (latitude < 0)
            northing += 10000000;

        return new(zone, latitude >= 0 ? 'N' : 'S', LatitudeBands[(int)Math.Floor((latitude + 80) / 8)], easting, northing);
    }

    private static string FormatMgrs(UtmCoordinate coordinate)
    {
        var column = (int)Math.Floor(coordinate.Easting / 100000) - 1;
        var row = (int)Math.Floor(coordinate.Northing / 100000) % 20;
        var columnLetter = EastingLetters[(coordinate.Zone - 1) % 3][column];
        var rowLetter = NorthingLetters[(coordinate.Zone - 1) % 2][row];
        var easting = (int)Math.Floor(coordinate.Easting % 100000);
        var northing = (int)Math.Floor(coordinate.Northing % 100000);
        return string.Create(CultureInfo.InvariantCulture, $"MGRS: {coordinate.Zone}{coordinate.Band} {columnLetter}{rowLetter} {easting:00000} {northing:00000}");
    }

    private static double DegreesToRadians(double value) => value * Math.PI / 180;

    private readonly record struct UtmCoordinate(int Zone, char Hemisphere, char Band, double Easting, double Northing);
}
