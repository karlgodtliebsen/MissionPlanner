using System.Buffers.Binary;
using System.Text;
using MissionPlanner.MavLink;
using MissionPlanner.MavLink.Services;

namespace MissionPlanner.Core.Tests.Fixtures;

/// <summary>Small reproducible bench logs encoded as standard timestamped MAVLink 2 packets.</summary>
internal static class BenchReplayFixtures
{
    internal static readonly DateTimeOffset Start = DateTimeOffset.Parse("2026-09-16T12:00:00Z");
    private static readonly CommonMavLinkCrcExtraProvider crc = new();

    internal static byte[] Heartbeat(bool armed = false) =>
        MavLinkKnownFrames.CreateHeartbeatV2(crc, baseMode: armed ? (byte)128 : (byte)0);

    internal static byte[] Health(bool ready)
    {
        var payload = new byte[31];
        const uint preArm = 1u << 28;
        BinaryPrimitives.WriteUInt32LittleEndian(payload, preArm);
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(4), preArm);
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(8), ready ? preArm : 0);
        return Packet(1, payload);
    }

    internal static byte[] Text(string text, ushort id = 0, byte chunk = 0)
    {
        var payload = new byte[54];
        payload[0] = 4;
        Encoding.ASCII.GetBytes(text).AsSpan(0, Math.Min(50, text.Length)).CopyTo(payload.AsSpan(1));
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(51), id);
        payload[53] = chunk;
        return Packet(253, payload);
    }

    internal static MemoryStream Log(params byte[][] packets) =>
        TimedLog(packets.Select((packet, index) => (index * 0.1, packet)).ToArray());

    internal static MemoryStream TimedLog(params (double Seconds, byte[] Packet)[] packets)
    {
        var stream = new MemoryStream();
        Span<byte> timestamp = stackalloc byte[8];
        foreach (var packet in packets)
        {
            BinaryPrimitives.WriteUInt64BigEndian(timestamp,
                checked((ulong)((Start.AddSeconds(packet.Seconds) - DateTimeOffset.UnixEpoch).Ticks / 10)));
            stream.Write(timestamp);
            stream.Write(packet.Packet);
        }
        stream.Position = 0;
        return stream;
    }

    private static byte[] Packet(byte messageId, byte[] payload)
    {
        var packet = new byte[12 + payload.Length];
        packet[0] = 0xfd;
        packet[1] = (byte)payload.Length;
        packet[5] = 1;
        packet[6] = 1;
        packet[7] = messageId;
        payload.CopyTo(packet, 10);
        if (!crc.TryGetCrcExtra(messageId, out var extra))
        {
            throw new InvalidOperationException("Fixture message has no CRC definition.");
        }
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(10 + payload.Length),
            MavLinkCrc.Calculate(packet.AsSpan(1, 9 + payload.Length), extra));
        return packet;
    }
}
