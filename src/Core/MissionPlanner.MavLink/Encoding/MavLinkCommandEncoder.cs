using System.Buffers.Binary;
using MissionPlanner.MavLink.Commands;
using MissionPlanner.MavLink.Messages;
using MissionPlanner.MavLink.Services.Abstractions;

namespace MissionPlanner.MavLink.Encoding;

/// <inheritdoc />
public sealed class MavLinkCommandEncoder(IMavLinkCrcExtraProvider crcExtraProvider) : IMavLinkCommandEncoder
{
    private byte sequence;

    /// <inheritdoc />
    public byte[] EncodeArmDisarm(byte targetSystemId, byte targetComponentId, bool arm)
    {
        Span<byte> payload = stackalloc byte[33];

        // COMMAND_LONG payload layout:
        // float param1..param7
        // uint16 command
        // uint8 target_system
        // uint8 target_component
        // uint8 confirmation

        WriteFloat(payload[0..4], arm ? 1.0f : 0.0f); // param1
        WriteFloat(payload[4..8], 0);
        WriteFloat(payload[8..12], 0);
        WriteFloat(payload[12..16], 0);
        WriteFloat(payload[16..20], 0);
        WriteFloat(payload[20..24], 0);
        WriteFloat(payload[24..28], 0);

        BinaryPrimitives.WriteUInt16LittleEndian(
            payload[28..30],
            MavLinkCommandIds.ComponentArmDisarm);

        payload[30] = targetSystemId;
        payload[31] = targetComponentId;
        payload[32] = 0; // confirmation

        return BuildV2Packet(
            255, // GCS
            190, // MAV_COMP_ID_MISSIONPLANNER-ish
            MessageIds.CommandLong,
            payload);
    }


    /// <inheritdoc />
    public byte[] EncodeSetMode(byte targetSystemId, byte targetComponentId, uint customMode)
    {
        Span<byte> payload = stackalloc byte[33];

        // COMMAND_LONG payload:
        // float param1..param7
        // uint16 command
        // uint8 target_system
        // uint8 target_component
        // uint8 confirmation
        //
        // MAV_CMD_DO_SET_MODE:
        // param1 = MAV_MODE_FLAG_CUSTOM_MODE_ENABLED
        // param2 = custom mode
        // param3 = custom submode, unused here

        WriteFloat(payload[0..4], 1.0f); // param1
        WriteFloat(payload[4..8], customMode); // param2
        WriteFloat(payload[8..12], 0);
        WriteFloat(payload[12..16], 0);
        WriteFloat(payload[16..20], 0);
        WriteFloat(payload[20..24], 0);
        WriteFloat(payload[24..28], 0);

        BinaryPrimitives.WriteUInt16LittleEndian(payload[28..30], MavLinkCommandIds.DoSetMode);

        payload[30] = targetSystemId;
        payload[31] = targetComponentId;
        payload[32] = 0;

        return BuildV2Packet(255, 190, MessageIds.CommandLong, payload);
    }


    private byte[] BuildV2Packet(byte systemId, byte componentId, uint messageId, ReadOnlySpan<byte> payload)
    {
        var packetLength = 10 + payload.Length + 2;
        var packet = new byte[packetLength];

        packet[0] = 0xFD;
        packet[1] = (byte)payload.Length;
        packet[2] = 0x00;
        packet[3] = 0x00;
        packet[4] = sequence++;
        packet[5] = systemId;
        packet[6] = componentId;
        packet[7] = (byte)(messageId & 0xFF);
        packet[8] = (byte)((messageId >> 8) & 0xFF);
        packet[9] = (byte)((messageId >> 16) & 0xFF);

        payload.CopyTo(packet.AsSpan(10));

        if (!crcExtraProvider.TryGetCrcExtra(messageId, out var crcExtra)) throw new InvalidOperationException($"No CRC extra registered for MAVLink message id {messageId}.");

        var crc = MavLinkCrc.Calculate(
            packet.AsSpan(1, 9 + payload.Length),
            crcExtra);

        var crcOffset = 10 + payload.Length;
        packet[crcOffset] = (byte)(crc & 0xFF);
        packet[crcOffset + 1] = (byte)((crc >> 8) & 0xFF);

        return packet;
    }

    private static void WriteFloat(Span<byte> target, float value)
    {
        var bytes = BitConverter.GetBytes(value);

        if (!BitConverter.IsLittleEndian) Array.Reverse(bytes);

        bytes.CopyTo(target);
    }
}