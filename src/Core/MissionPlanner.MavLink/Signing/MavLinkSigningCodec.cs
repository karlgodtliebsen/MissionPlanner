using System.Security.Cryptography;

namespace MissionPlanner.MavLink.Signing;

/// <summary>Implements MAVLink 2's SHA-256/48 signature and 48-bit timestamp encoding.</summary>
public static class MavLinkSigningCodec
{
    /// <summary>Largest timestamp representable on the wire.</summary>
    public const ulong MaximumTimestamp = (1UL << 48) - 1;
    /// <summary>MAVLink signing's UTC epoch.</summary>
    public static readonly DateTimeOffset Epoch = new(2015, 1, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>Converts UTC to ten-microsecond units, rejecting underflow and wraparound.</summary>
    public static ulong Timestamp(DateTimeOffset utc)
    {
        var ticks = (utc - Epoch).Ticks;
        if (ticks < 0 || (ulong)(ticks / 100) > MaximumTimestamp)
        {
            throw new ArgumentOutOfRangeException(nameof(utc), "Signing time is outside its supported epoch.");
        }
        return (ulong)(ticks / 100);
    }

    /// <summary>Validates an explicitly supplied 64-character hexadecimal secret without echoing it.</summary>
    public static byte[] Import(string text)
    {
        if (text.Length != 64 || !text.All(Uri.IsHexDigit))
        {
            throw new ArgumentException("A signing key must contain exactly 64 hexadecimal characters.");
        }
        return Convert.FromHexString(text);
    }

    /// <summary>Creates a cryptographically random 32-byte key.</summary>
    public static byte[] Generate() => RandomNumberGenerator.GetBytes(32);

    /// <summary>Returns a short SHA-256 fingerprint, never the secret.</summary>
    public static string Fingerprint(ReadOnlySpan<byte> key)
    {
        ValidateKey(key);
        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(key, hash);
        return Convert.ToHexString(hash[..8]);
    }

    /// <summary>Signs one unsigned MAVLink 2 packet, including the signed flag in its recalculated CRC.</summary>
    public static byte[] Sign(ReadOnlySpan<byte> packet, ReadOnlySpan<byte> key, byte crcExtra, byte linkId, ulong timestamp)
    {
        ValidateKey(key);
        if (timestamp > MaximumTimestamp)
        {
            throw new ArgumentOutOfRangeException(nameof(timestamp));
        }
        if (packet.Length < 12 || packet[0] != 0xfd || packet.Length != packet[1] + 12 || packet[2] != 0)
        {
            throw new ArgumentException("Signing requires one unsigned MAVLink 2 packet with supported flags.");
        }
        var output = new byte[packet.Length + 13];
        packet.CopyTo(output);
        output[2] = 1;
        var crc = MavLinkCrc.Calculate(output.AsSpan(1, packet.Length - 3), crcExtra);
        output[packet.Length - 2] = (byte)crc;
        output[packet.Length - 1] = (byte)(crc >> 8);
        output[packet.Length] = linkId;
        for (var index = 0; index < 6; index++)
        {
            output[packet.Length + 1 + index] = (byte)(timestamp >> (8 * index));
        }
        Digest(output.AsSpan(0, output.Length - 6), key, output.AsSpan(output.Length - 6));
        return output;
    }

    /// <summary>Checks a signature in constant time; CRC validation belongs to the existing frame parser.</summary>
    public static bool Verify(ReadOnlySpan<byte> packet, ReadOnlySpan<byte> key)
    {
        ValidateKey(key);
        if (packet.Length < 25 || packet[0] != 0xfd || packet[2] != 1 || packet.Length != packet[1] + 25)
        {
            return false;
        }
        Span<byte> expected = stackalloc byte[6];
        Digest(packet[..^6], key, expected);
        return CryptographicOperations.FixedTimeEquals(expected, packet[^6..]);
    }

    /// <summary>Reads the timestamp from a structurally valid signed packet.</summary>
    public static ulong ReadTimestamp(ReadOnlySpan<byte> packet)
    {
        if (packet.Length < 25 || packet[0] != 0xfd || packet[2] != 1 || packet.Length != packet[1] + 25)
        {
            throw new ArgumentException("Malformed signed packet.");
        }
        ulong timestamp = 0;
        for (var index = 0; index < 6; index++)
        {
            timestamp |= (ulong)packet[packet.Length - 12 + index] << (index * 8);
        }
        return timestamp;
    }

    private static void ValidateKey(ReadOnlySpan<byte> key)
    {
        if (key.Length != 32)
        {
            throw new ArgumentException("A signing key must contain exactly 32 bytes.");
        }
    }

    private static void Digest(ReadOnlySpan<byte> packet, ReadOnlySpan<byte> key, Span<byte> destination)
    {
        Span<byte> buffer = stackalloc byte[32 + 280];
        Span<byte> hash = stackalloc byte[32];
        try
        {
            key.CopyTo(buffer);
            packet.CopyTo(buffer[32..]);
            SHA256.HashData(buffer[..(32 + packet.Length)], hash);
            hash[..6].CopyTo(destination);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(buffer);
            CryptographicOperations.ZeroMemory(hash);
        }
    }
}
