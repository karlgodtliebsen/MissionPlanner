using System.Buffers.Binary;
using MissionPlanner.MavLink.Messages;
using MissionPlanner.MavLink.Services;

namespace MissionPlanner.MavLink;

/// <summary>
/// 
/// </summary>
public static class MavLinkKnownFrames
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="crcExtraProvider"></param>
    /// <param name="sequence"></param>
    /// <param name="systemId"></param>
    /// <param name="componentId"></param>
    /// <param name="customMode"></param>
    /// <param name="vehicleType"></param>
    /// <param name="autopilot"></param>
    /// <param name="baseMode"></param>
    /// <param name="systemStatus"></param>
    /// <param name="mavLinkVersion"></param>
    /// <returns></returns>
    public static byte[] CreateHeartbeatV2(
        IMavLinkCrcExtraProvider crcExtraProvider,
        byte sequence = 0,
        byte systemId = 1,
        byte componentId = 1,
        uint customMode = 0,
        byte vehicleType = 2,
        byte autopilot = 3,
        byte baseMode = 0,
        byte systemStatus = 4,
        byte mavLinkVersion = 3)
    {
        Span<byte> payload = stackalloc byte[9];

        BinaryPrimitives.WriteUInt32LittleEndian(
            payload[0..4],
            customMode);

        payload[4] = vehicleType;
        payload[5] = autopilot;
        payload[6] = baseMode;
        payload[7] = systemStatus;
        payload[8] = mavLinkVersion;

        return BuildV2Packet(
            crcExtraProvider,
            sequence,
            systemId,
            componentId,
            MessageIds.Heartbeat,
            payload);
    }

    private static byte[] BuildV2Packet(
        IMavLinkCrcExtraProvider crcExtraProvider,
        byte sequence,
        byte systemId,
        byte componentId,
        uint messageId,
        ReadOnlySpan<byte> payload)
    {
        var packet = new byte[10 + payload.Length + 2];

        packet[0] = 0xFD;
        packet[1] = (byte)payload.Length;
        packet[2] = 0x00;
        packet[3] = 0x00;
        packet[4] = sequence;
        packet[5] = systemId;
        packet[6] = componentId;
        packet[7] = (byte)(messageId & 0xFF);
        packet[8] = (byte)((messageId >> 8) & 0xFF);
        packet[9] = (byte)((messageId >> 16) & 0xFF);

        payload.CopyTo(packet.AsSpan(10));

        if (!crcExtraProvider.TryGetCrcExtra(messageId, out var crcExtra))
            throw new InvalidOperationException(
                $"No CRC extra registered for message {messageId}");

        var crc = MavLinkCrc.Calculate(
            packet.AsSpan(1, 9 + payload.Length),
            crcExtra);

        var crcOffset = 10 + payload.Length;

        packet[crcOffset] = (byte)(crc & 0xFF);
        packet[crcOffset + 1] = (byte)(crc >> 8);

        return packet;
    }
}