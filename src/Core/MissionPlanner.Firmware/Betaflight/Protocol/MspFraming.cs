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

/// <summary>Incremental response parser that retains at most one bounded frame.</summary>
public sealed class MspFrameParser
{
    private readonly List<byte> pending = new(MspFraming.MaximumPayload + 9);

    /// <summary>Gets the most recent framing fault, retained until a valid frame is returned.</summary>
    public MspFailure Failure { get; private set; }

    /// <summary>Feeds one byte and returns a complete valid response when available.</summary>
    public MspFrame? Push(byte value)
    {
        pending.Add(value);
        while (pending.Count > 0)
        {
            if (pending[0] != '$')
            {
                pending.RemoveAt(0);
                continue;
            }
            if (pending.Count < 3)
            {
                return null;
            }
            var v2 = pending[1] == 'X';
            if ((!v2 && pending[1] != 'M') || (pending[2] != '>' && pending[2] != '!'))
            {
                pending.RemoveAt(0);
                continue;
            }
            var header = v2 ? 8 : 5;
            if (pending.Count < header)
            {
                return null;
            }
            var length = v2 ? pending[6] | pending[7] << 8 : pending[3];
            if (length > MspFraming.MaximumPayload || (!v2 && length == 255) || (v2 && pending[3] != 0))
            {
                Failure = MspFailure.MalformedFrame;
                pending.RemoveAt(0);
                continue;
            }
            var size = header + length + 1;
            if (pending.Count < size)
            {
                return null;
            }
            var bytes = pending.Take(size).ToArray();
            if (MspFraming.Checksum(bytes.AsSpan(3, size - 4), v2) != bytes[^1])
            {
                Failure = MspFailure.ChecksumFailure;
                pending.RemoveAt(0);
                continue;
            }
            pending.RemoveRange(0, size);
            Failure = MspFailure.None;
            return new(v2 ? MspProtocolVersion.V2 : MspProtocolVersion.V1,
                (ushort)(v2 ? bytes[4] | bytes[5] << 8 : bytes[4]), bytes.AsSpan(header, length).ToArray(), bytes[2] == '!');
        }
        return null;
    }
}
