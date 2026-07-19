using System.Buffers.Binary;

namespace MissionPlanner.MavLink.MavFtp;

public sealed class MavFtpPacketCodec : IMavFtpPacketCodec
{
    public const int HeaderLength = 12;
    public const int PayloadLength = 251;
    public int MaximumDataLength => PayloadLength - HeaderLength;

    public byte[] Encode(MavFtpPacket packet)
    {
        if (packet.Data.Length > MaximumDataLength)
        {
            throw new ArgumentOutOfRangeException(nameof(packet), $"MAVFTP data cannot exceed {MaximumDataLength} bytes.");
        }

        var result = new byte[HeaderLength + packet.Data.Length];
        BinaryPrimitives.WriteUInt16LittleEndian(result, packet.Sequence);
        result[2] = packet.Session;
        result[3] = (byte)packet.Opcode;
        result[4] = checked((byte)packet.Data.Length);
        result[5] = (byte)packet.RequestedOpcode;
        result[6] = packet.BurstComplete ? (byte)1 : (byte)0;
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(8), packet.Offset);
        packet.Data.Span.CopyTo(result.AsSpan(HeaderLength));
        return result;
    }

    public MavFtpPacket Decode(ReadOnlySpan<byte> payload)
    {
        if (payload.Length < HeaderLength)
        {
            throw new MavFtpProtocolException("MAVFTP payload is truncated.");
        }

        var size = payload[4];
        if (size > MaximumDataLength || HeaderLength + size > payload.Length)
        {
            throw new MavFtpProtocolException("MAVFTP payload declares an invalid data size.");
        }

        var opcode = (MavFtpOpcode)payload[3];
        if (!Enum.IsDefined(opcode))
        {
            throw new MavFtpProtocolException($"Unknown MAVFTP opcode {payload[3]}.");
        }

        return new MavFtpPacket(
            BinaryPrimitives.ReadUInt16LittleEndian(payload), payload[2], opcode,
            (MavFtpOpcode)payload[5], payload[6] != 0,
            BinaryPrimitives.ReadUInt32LittleEndian(payload[8..]), payload.Slice(HeaderLength, size).ToArray());
    }
}
