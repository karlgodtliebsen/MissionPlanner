using System.Globalization;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Firmware;

namespace MissionPlanner.Core.Setup.Advanced.Nmea;

/// <summary>Configures coalesced GGA/RMC output without fabricating unavailable measurements.</summary>
public sealed record NmeaOptions(bool Gga = true, bool Rmc = true, int RateHz = 1)
{
    /// <summary>Validates the sentence selection and one-to-ten-Hz output range.</summary>
    public void Validate()
    {
        if ((!Gga && !Rmc) || RateHz is < 1 or > 10) { throw new ArgumentException("Select at least one sentence and an output rate from 1 to 10 Hz."); }
    }
}

/// <summary>A complete ASCII sentence batch and its freshness explanation.</summary>
public sealed record NmeaBatch(string Text, int SentenceCount, bool ValidFix, string Status);

/// <summary>Formats receiver observations using invariant NMEA coordinates, checksums and line endings.</summary>
public static class NmeaFormatter
{
    private static readonly CultureInfo invariant = CultureInfo.InvariantCulture;
    /// <summary>Gets the maximum age of receiver and satellite-use observations.</summary>
    public static readonly TimeSpan MaximumAge = TimeSpan.FromSeconds(3);

    /// <summary>Builds GGA/RMC using receiver coordinates and UTC reception time; stale fields are blank.</summary>
    public static NmeaBatch Format(VehicleState? state, DateTimeOffset now, NmeaOptions options)
    {
        options.Validate();
        var gps = state?.Gps;
        var fresh = Fresh(gps?.ObservedAt, now);
        var quality = fresh ? Quality(gps!.FixType) : 0;
        var valid = quality != 0 && Finite(gps!.LatitudeDegrees, -90, 90) && Finite(gps.LongitudeDegrees, -180, 180);
        if (!valid) { quality = 0; }
        var latitude = valid ? Coordinate(gps!.LatitudeDegrees!.Value, false) : "";
        var longitude = valid ? Coordinate(gps!.LongitudeDegrees!.Value, true) : "";
        var northSouth = valid ? gps!.LatitudeDegrees < 0 ? "S" : "N" : "";
        var eastWest = valid ? gps!.LongitudeDegrees < 0 ? "W" : "E" : "";
        var utc = fresh ? gps!.ObservedAt!.Value.UtcDateTime : (DateTime?)null;
        var time = utc?.ToString("HHmmss.ff", invariant) ?? "";
        var date = utc?.ToString("ddMMyy", invariant) ?? "";
        var sentences = new List<string>(2);
        if (options.Gga)
        {
            var satellites = valid && Fresh(gps!.SatelliteUsageObservedAt, now) && gps.SatellitesUsed is >= 0 and <= 99
                ? gps.SatellitesUsed.Value.ToString("00", invariant) : "";
            var hdop = valid ? Number(gps!.HorizontalDilution, 0, 999.9, "0.0") : "";
            var altitude = valid ? Number(gps!.AltitudeMslMeters, -10000, 100000, "0.0") : "";
            var separation = valid ? Number(gps!.GeoidSeparationMeters, -1000, 1000, "0.0") : "";
            sentences.Add(Sentence($"GPGGA,{time},{latitude},{northSouth},{longitude},{eastWest},{quality},{satellites},{hdop},{altitude},M,{separation},M,,"));
        }
        if (options.Rmc)
        {
            var speed = valid && Finite(gps!.GroundSpeedMetersPerSecond, 0, 10000)
                ? (gps.GroundSpeedMetersPerSecond!.Value * 3600 / 1852).ToString("0.00", invariant) : "";
            var course = valid && Finite(gps!.CourseDegrees, 0, 360)
                ? (Math.Round(gps.CourseDegrees!.Value, 2) % 360).ToString("0.00", invariant) : "";
            sentences.Add(Sentence($"GPRMC,{time},{(valid ? "A" : "V")},{latitude},{northSouth},{longitude},{eastWest},{speed},{course},{date},,"));
        }
        return new(string.Concat(sentences), sentences.Count, valid,
            valid ? "Current receiver fix; unavailable fields are blank. UTC is receiver-message reception time."
                : "No valid current receiver fix; GGA quality 0 / RMC void with blank measurements.");
    }

    /// <summary>Formats degrees/minutes with carry at rounded minute boundaries.</summary>
    public static string Coordinate(double degrees, bool longitude)
    {
        if (!double.IsFinite(degrees) || Math.Abs(degrees) > (longitude ? 180 : 90)) { throw new ArgumentOutOfRangeException(nameof(degrees)); }
        var minutes = Math.Round(Math.Abs(degrees) * 60, 4, MidpointRounding.AwayFromZero);
        var wholeDegrees = (int)(minutes / 60);
        return wholeDegrees.ToString(longitude ? "000" : "00", invariant) + (minutes - wholeDegrees * 60).ToString("00.0000", invariant);
    }

    /// <summary>Wraps an ASCII NMEA body with its XOR checksum and CRLF.</summary>
    public static string Sentence(string body)
    {
        if (body.Any(character => character is < ' ' or > '~' or '$' or '*')) { throw new ArgumentException("NMEA body must contain printable ASCII without framing delimiters."); }
        byte checksum = 0;
        foreach (var character in body) { checksum ^= (byte)character; }
        return $"${body}*{checksum:X2}\r\n";
    }

    private static bool Fresh(DateTimeOffset? observed, DateTimeOffset now) => observed is { } time && time <= now && now - time <= MaximumAge;
    private static bool Finite(double? value, double min, double max) => value is { } number && double.IsFinite(number) && number >= min && number <= max;
    private static string Number(double? value, double min, double max, string format) => Finite(value, min, max) ? value!.Value.ToString(format, invariant) : "";
    private static int Quality(GpsFixType fix) => fix switch
    {
        GpsFixType.Fix2D or GpsFixType.Fix3D => 1,
        GpsFixType.DifferentialGps => 2,
        GpsFixType.RtkFixed => 4,
        GpsFixType.RtkFloat => 5,
        _ => 0 // Static/PPP modes lack enough provenance to invent a GGA quality classification.
    };
}
