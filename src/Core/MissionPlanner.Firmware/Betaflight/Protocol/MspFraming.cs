using System.Buffers.Binary;

namespace MissionPlanner.Firmware.Betaflight.Protocol;

/// <summary>Bounded native MSP framing, independent of serial and MAVLink.</summary>
public static class MspFraming
{
    /// <summary>Maximum accepted native v2 payload; v1 remains limited to 254 bytes.</summary>
    public const int MaximumPayload = 1024;

    /// <summary>Encodes a request using v1 for small commands and native v2 otherwise.</summary>
    public static byte[] EncodeRequest(ushort command, ReadOnlySpan<byte> payload)
    {
        if (payload.Length > MaximumPayload)
        {
            throw new ArgumentOutOfRangeException(nameof(payload));
        }
        var v2 = command > 254 || payload.Length > 254;
        var bytes = new byte[payload.Length + (v2 ? 9 : 6)];
        bytes[0] = (byte)'$';
        bytes[1] = (byte)(v2 ? 'X' : 'M');
        bytes[2] = (byte)'<';
        if (v2)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(4), command);
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(6), (ushort)payload.Length);
            payload.CopyTo(bytes.AsSpan(8));
        }
        else
        {
            bytes[3] = (byte)payload.Length;
            bytes[4] = (byte)command;
            payload.CopyTo(bytes.AsSpan(5));
        }
        bytes[^1] = Checksum(bytes.AsSpan(3, bytes.Length - 4), v2);
        return bytes;
    }

    internal static byte Checksum(ReadOnlySpan<byte> bytes, bool v2)
    {
        byte crc = 0;
        foreach (var value in bytes)
        {
            crc ^= value;
            if (v2)
            {
                for (var bit = 0; bit < 8; bit++)
                {
                    crc = (byte)((crc << 1) ^ ((crc & 0x80) != 0 ? 0xd5 : 0));
                }
            }
        }
        return crc;
    }
}