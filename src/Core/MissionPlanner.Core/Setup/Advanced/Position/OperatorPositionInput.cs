using System.Globalization;
using System.Text;
using MissionPlanner.Core.Missions.Models;
using MissionPlanner.Core.Setup.OptionalHardware;

namespace MissionPlanner.Core.Setup.Advanced.Position;

/// <summary>A checksum-validated GGA fix and its observation times; invalid fixes never retain old coordinates.</summary>
public sealed record OperatorPositionFix(GeoPosition Position, double? AltitudeMsl, int Satellites, double Hdop,
    DateTimeOffset ObservedAt, DateTimeOffset ReceiverTime, bool Valid, string Status)
{
    /// <summary>Gets whether a real fix is recent enough for navigation.</summary>
    public bool IsFresh(DateTimeOffset now) => Valid && now >= ObservedAt && now - ObservedAt <= TimeSpan.FromSeconds(3) &&
        Math.Abs((now - ReceiverTime).TotalSeconds) <= 10;
}

/// <summary>Parses the supported GGA sentences with checksum, coordinate, quality and UTC-age checks.</summary>
public static class OperatorNmeaParser
{
    /// <summary>Returns null for unrelated sentences and an explicitly invalid observation for rejected GGA data.</summary>
    public static OperatorPositionFix? Parse(string sentence, DateTimeOffset now)
    {
        if (!sentence.StartsWith("$GPGGA,", StringComparison.Ordinal) && !sentence.StartsWith("$GNGGA,", StringComparison.Ordinal)) return null;
        OperatorPositionFix Invalid(string reason) => new(default, null, 0, double.NaN, now, now, false, reason);
        var star = sentence.IndexOf('*');
        if (sentence.Length > 256 || star < 0 || star != sentence.Length - 3 || !byte.TryParse(sentence.AsSpan(star + 1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var checksum)) return Invalid("Invalid GGA framing.");
        byte actual = 0;
        foreach (var c in sentence.AsSpan(1, star - 1)) { if (c > 127) return Invalid("Non-ASCII NMEA input."); actual ^= (byte)c; }
        if (actual != checksum) return Invalid("GGA checksum failed.");
        var fields = sentence[1..star].Split(',');
        if (fields.Length < 15) return Invalid("Incomplete GGA sentence.");
        double Number(int index) => double.TryParse(fields[index], NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && double.IsFinite(value) ? value : double.NaN;
        var quality = Number(6);
        var satellites = Number(7);
        var hdop = Number(8);
        if (quality is not (1 or 2 or 4 or 5) || satellites is < 4 or > 99 || satellites != Math.Truncate(satellites) || !double.IsFinite(satellites) || hdop is <= 0 or > 4 || !double.IsFinite(hdop)) return Invalid("No sufficiently accurate GPS fix (at least four satellites and HDOP ≤ 4 required).");
        double Coordinate(int index, int signIndex, int maximum)
        {
            var raw = Number(index);
            var degrees = Math.Floor(raw / 100);
            var minutes = raw - degrees * 100;
            if (!double.IsFinite(raw) || raw < 0 || minutes >= 60 || degrees > maximum || degrees == maximum && minutes > 0) return double.NaN;
            var value = degrees + minutes / 60;
            var sign = fields[signIndex];
            return sign == (maximum == 90 ? "N" : "E") ? value : sign == (maximum == 90 ? "S" : "W") ? -value : double.NaN;
        }
        var position = new GeoPosition(Coordinate(2, 3, 90), Coordinate(4, 5, 180));
        if (!position.IsValid) return Invalid("Invalid GPS coordinate.");
        var utc = fields[1];
        if (utc.Length < 6 || !int.TryParse(utc.AsSpan(0, 2), out var hour) || !int.TryParse(utc.AsSpan(2, 2), out var minute) ||
            !double.TryParse(utc.AsSpan(4), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var second) ||
            hour is < 0 or > 23 || minute is < 0 or > 59 || second is < 0 or >= 60 || !double.IsFinite(second)) return Invalid("Invalid GPS UTC time.");
        var receiverTime = new DateTimeOffset(now.UtcDateTime.Date, TimeSpan.Zero).AddHours(hour).AddMinutes(minute).AddSeconds(second);
        if (receiverTime - now > TimeSpan.FromHours(12)) receiverTime = receiverTime.AddDays(-1);
        if (now - receiverTime > TimeSpan.FromHours(12)) receiverTime = receiverTime.AddDays(1);
        if (Math.Abs((now - receiverTime).TotalSeconds) > 10) return Invalid("GPS UTC is stale or the computer clock is incorrect.");
        var altitude = Number(9);
        double? msl = fields[10] == "M" && altitude is >= -1000 and <= 20000 ? altitude : null;
        return new(position, msl, (int)satellites, hdop, now, receiverTime, true, "Current checksum-validated GPS fix.");
    }
}

/// <summary>Owns one bounded serial/TCP/UDP NMEA input. Reading never sends commands to a vehicle.</summary>
public sealed class OperatorPositionInput(IRtkCorrectionSourceFactory sources)
{
    private OperatorPositionFix? current;
    private int running;
    private long generation;
    /// <summary>Gets the most recent GGA observation, including explicit invalid-fix reports.</summary>
    public OperatorPositionFix? Current => Volatile.Read(ref current);
    /// <summary>Identifies each input lifetime so navigation cannot migrate silently to another source.</summary>
    public long Generation => Interlocked.Read(ref generation);

    /// <summary>Reads until stopped or the source closes/times out; the latest fix is cleared on exit.</summary>
    public async Task RunAsync(RtkSourceOptions options, IProgress<OperatorPositionFix> progress, CancellationToken token)
    {
        if (options.Kind is not (RtkSourceKind.Serial or RtkSourceKind.Tcp or RtkSourceKind.Udp)) throw new ArgumentException("Select serial, TCP client or UDP listener for NMEA input.");
        if (Interlocked.CompareExchange(ref running, 1, 0) != 0) throw new InvalidOperationException("Stop the current GPS source before opening another.");
        Interlocked.Increment(ref generation);
        Volatile.Write(ref current, null);
        try
        {
            using var opening = CancellationTokenSource.CreateLinkedTokenSource(token);
            opening.CancelAfter(TimeSpan.FromSeconds(10));
            await using var source = await sources.OpenAsync(options, opening.Token).ConfigureAwait(false);
            var buffer = new byte[2048];
            var line = new StringBuilder(256);
            var overflow = false;
            while (true)
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
                timeout.CancelAfter(TimeSpan.FromSeconds(5));
                var count = await source.ReadAsync(buffer, timeout.Token).ConfigureAwait(false);
                if (count == 0) throw new EndOfStreamException("The operator GPS source closed.");
                foreach (var b in buffer.AsSpan(0, count))
                {
                    if (b == '\n')
                    {
                        if (!overflow && OperatorNmeaParser.Parse(line.ToString().TrimEnd('\r'), DateTimeOffset.UtcNow) is { } fix)
                        {
                            var prior = Current;
                            if (fix.Valid && prior is { Valid: true } && fix.ReceiverTime == prior.ReceiverTime)
                                fix = fix with { ObservedAt = prior.ObservedAt }; // Replayed sentences cannot refresh the fix age.
                            Volatile.Write(ref current, fix); progress.Report(fix);
                        }
                        line.Clear(); overflow = false;
                    }
                    else if (!overflow)
                    {
                        if (line.Length >= 256) { line.Clear(); overflow = true; }
                        else line.Append((char)b);
                    }
                }
            }
        }
        finally { Volatile.Write(ref current, null); Volatile.Write(ref running, 0); }
    }
}
