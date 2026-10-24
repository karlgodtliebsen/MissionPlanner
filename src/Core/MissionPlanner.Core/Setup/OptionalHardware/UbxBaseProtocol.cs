using System.Buffers.Binary;
using MissionPlanner.Core.Missions.Models;

namespace MissionPlanner.Core.Setup.OptionalHardware;

/// <summary>A checksum-validated UBX message.</summary>
public sealed record UbxPacket(byte Class, byte Id, byte[] Payload);

/// <summary>Frames a mixed GNSS byte stream, bounding UBX payloads and resynchronizing after corruption.</summary>
public sealed class UbxFramer
{
    private readonly List<byte> buffer = [];
    /// <summary>Gets the rejected UBX checksum count.</summary>
    public long ChecksumErrors { get; private set; }
    /// <summary>Accepts bytes and emits valid UBX messages with payloads up to 4096 bytes.</summary>
    public IReadOnlyList<UbxPacket> Push(ReadOnlySpan<byte> bytes)
    {
        var result = new List<UbxPacket>();
        foreach (var value in bytes)
        {
            buffer.Add(value);
            while (buffer.Count >= 2 && (buffer[0] != 0xB5 || buffer[1] != 0x62)) buffer.RemoveAt(0);
            if (buffer.Count < 6) continue;
            var length = buffer[4] | buffer[5] << 8;
            if (length > 4096) { buffer.RemoveAt(0); continue; }
            if (buffer.Count < length + 8) continue;
            byte a = 0, b = 0;
            for (var i = 2; i < length + 6; i++) { a = unchecked((byte)(a + buffer[i])); b = unchecked((byte)(b + a)); }
            if (buffer[length + 6] == a && buffer[length + 7] == b)
            {
                result.Add(new(buffer[2], buffer[3], buffer.Skip(6).Take(length).ToArray())); buffer.RemoveRange(0, length + 8);
            }
            else { ChecksumErrors++; buffer.RemoveAt(0); }
        }
        return result;
    }
    /// <summary>Builds a UBX frame with its two-byte Fletcher checksum.</summary>
    public static byte[] Encode(byte messageClass, byte id, ReadOnlySpan<byte> payload)
    {
        if (payload.Length > 4096) throw new ArgumentOutOfRangeException(nameof(payload));
        var frame = new byte[payload.Length + 8]; frame[0] = 0xB5; frame[1] = 0x62; frame[2] = messageClass; frame[3] = id;
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(4), (ushort)payload.Length); payload.CopyTo(frame.AsSpan(6));
        byte a = 0, b = 0;
        for (var i = 2; i < frame.Length - 2; i++) { a = unchecked((byte)(a + frame[i])); b = unchecked((byte)(b + a)); }
        frame[^2] = a; frame[^1] = b; return frame;
    }
}

/// <summary>A named WGS84 antenna-reference-point position, with ellipsoid height and declared accuracy in metres.</summary>
public sealed record RtkBasePosition(string Name, double Latitude, double Longitude, double EllipsoidHeight, double Accuracy)
{
    /// <summary>Rejects invalid positions and unnamed or excessively imprecise presets.</summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Name) || Name.Length > 80 || Name.Any(char.IsControl) || !new GeoPosition(Latitude, Longitude).IsValid ||
            !double.IsFinite(EllipsoidHeight) || EllipsoidHeight is < -1000 or > 20000 || !double.IsFinite(Accuracy) || Accuracy is < .0001 or > 100)
            throw new ArgumentException("Use a name up to 80 characters, valid WGS84 coordinates, ellipsoid height -1000–20000 m and accuracy 0.0001–100 m.");
    }
}

/// <summary>Validated UBX-NAV-SVIN evidence; a completed survey is distinct from a correction stream.</summary>
public sealed record RtkSurveyStatus(uint Duration, uint Observations, double Accuracy, bool Valid, bool Active, RtkBasePosition? MeanPosition, DateTimeOffset ObservedAt);
/// <summary>One UBX-NAV-SAT satellite observation.</summary>
public sealed record RtkSatellite(string Constellation, byte Number, byte CarrierNoise, int Elevation, bool Used);

/// <summary>Encodes TMODE3 and decodes receiver diagnostics in explicitly documented units.</summary>
public static class UbxBaseProtocol
{
    /// <summary>Builds survey settings; duration and accuracy are completion thresholds, not a timer guarantee.</summary>
    public static byte[] Survey(uint seconds, double accuracy)
    {
        if (seconds is < 60 or > 604800 || !double.IsFinite(accuracy) || accuracy is < .01 or > 100) throw new ArgumentException("Survey duration must be 60–604800 seconds and accuracy 0.01–100 m.");
        var payload = new byte[40]; payload[2] = 1;
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(24), seconds);
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(28), checked((uint)Math.Round(accuracy * 10000)));
        return payload;
    }
    /// <summary>Builds fixed LLA settings with 1e-9 degree and 0.1 mm signed fine components.</summary>
    public static byte[] Fixed(RtkBasePosition position)
    {
        position.Validate(); var payload = new byte[40]; payload[2] = 2; payload[3] = 1;
        Coordinate(payload, 4, 16, position.Latitude * 1e9);
        Coordinate(payload, 8, 17, position.Longitude * 1e9);
        Coordinate(payload, 12, 18, position.EllipsoidHeight * 10000);
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(20), checked((uint)Math.Round(position.Accuracy * 10000)));
        return payload;
    }
    private static void Coordinate(byte[] payload, int coarseOffset, int fineOffset, double value)
    {
        var units = checked((long)Math.Round(value));
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(coarseOffset), checked((int)(units / 100)));
        payload[fineOffset] = unchecked((byte)(sbyte)(units % 100));
    }
    /// <summary>Decodes survey state and its ECEF mean to WGS84; invalid means cannot be saved as fixed positions.</summary>
    public static RtkSurveyStatus DecodeSurvey(ReadOnlySpan<byte> payload, DateTimeOffset now)
    {
        if (payload.Length != 40 || payload[0] != 0 || payload[36] > 1 || payload[37] > 1) throw new InvalidDataException("Unsupported NAV-SVIN payload.");
        var accuracy = BinaryPrimitives.ReadUInt32LittleEndian(payload[28..]) * .0001;
        RtkBasePosition? position = null;
        if (payload[36] == 1)
        {
            var x = BinaryPrimitives.ReadInt32LittleEndian(payload[12..]) * .01 + unchecked((sbyte)payload[24]) * .0001;
            var y = BinaryPrimitives.ReadInt32LittleEndian(payload[16..]) * .01 + unchecked((sbyte)payload[25]) * .0001;
            var z = BinaryPrimitives.ReadInt32LittleEndian(payload[20..]) * .01 + unchecked((sbyte)payload[26]) * .0001;
            position = EcefPosition(x, y, z, accuracy);
        }
        return new(BinaryPrimitives.ReadUInt32LittleEndian(payload[8..]), BinaryPrimitives.ReadUInt32LittleEndian(payload[32..]), accuracy,
            payload[36] == 1, payload[37] == 1, position, now);
    }
    private static RtkBasePosition? EcefPosition(double x, double y, double z, double accuracy)
    {
        const double a = 6378137, e2 = 6.6943799901413165e-3;
        var radius = Math.Sqrt(x * x + y * y + z * z);
        if (radius is < 6300000 or > 6400000 || accuracy is < .0001 or > 100) return null;
        var p = Math.Sqrt(x * x + y * y);
        var latitude = Math.Atan2(z, p * (1 - e2));
        for (var i = 0; i < 10; i++) { var n = a / Math.Sqrt(1 - e2 * Math.Pow(Math.Sin(latitude), 2)); latitude = Math.Atan2(z + e2 * n * Math.Sin(latitude), p); }
        var normal = a / Math.Sqrt(1 - e2 * Math.Pow(Math.Sin(latitude), 2));
        var height = p > .001 ? p / Math.Cos(latitude) - normal : Math.Abs(z) - normal * (1 - e2);
        if (height is < -1000 or > 20000) return null;
        return new("Survey result", latitude * 180 / Math.PI, Math.Atan2(y, x) * 180 / Math.PI, height, accuracy);
    }
    /// <summary>Decodes the bounded satellite list, retaining constellation, signal and solution-use flags.</summary>
    public static IReadOnlyList<RtkSatellite> DecodeSatellites(ReadOnlySpan<byte> payload)
    {
        if (payload.Length < 8 || payload[4] != 1 || payload.Length != 8 + payload[5] * 12) throw new InvalidDataException("Unsupported NAV-SAT payload.");
        var result = new List<RtkSatellite>();
        for (var i = 0; i < payload[5]; i++)
        {
            var block = payload[(8 + i * 12)..];
            result.Add(new(block[0] switch { 0 => "GPS", 1 => "SBAS", 2 => "Galileo", 3 => "BeiDou", 4 => "IMES", 5 => "QZSS", 6 => "GLONASS", 7 => "NavIC", _ => "Unknown" },
                block[1], block[2], unchecked((sbyte)block[3]), (BinaryPrimitives.ReadUInt32LittleEndian(block[8..]) & 8) != 0));
        }
        return result;
    }
}
