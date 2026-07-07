using System.Buffers.Binary;
using System.Text;
using MissionPlanner.MavLink.Messages;
using MissionPlanner.MavLink.Parameters;
using MissionPlanner.MavLink.Services;

namespace MissionPlanner.MavLink.Encoding;

/// <inheritdoc />
public sealed class MavLinkParameterEncoder(IMavLinkCrcExtraProvider crcExtraProvider) : IMavLinkParameterEncoder
{
    private byte sequence;

    /// <inheritdoc />
    public byte[] EncodeParamRequestList(byte targetSystemId, byte targetComponentId)
    {
        // PARAM_REQUEST_LIST payload layout (2 bytes):
        // uint8 target_system
        // uint8 target_component
        Span<byte> payload = stackalloc byte[2];
        payload[0] = targetSystemId;
        payload[1] = targetComponentId;

        return BuildV2Packet(
            255, // GCS
            190, // MAV_COMP_ID_MISSIONPLANNER
            MessageIds.ParamRequestList,
            payload);
    }

    /// <inheritdoc />
    public byte[] EncodeParamRequestRead(byte targetSystemId, byte targetComponentId, string paramId, short paramIndex = -1)
    {
        // PARAM_REQUEST_READ payload layout (20 bytes):
        // int16 param_index     (2 bytes, offset 0)
        // uint8 target_system   (1 byte, offset 2)
        // uint8 target_component (1 byte, offset 3)
        // char[16] param_id     (16 bytes, offset 4)

        Span<byte> payload = stackalloc byte[20];

        BinaryPrimitives.WriteInt16LittleEndian(payload[0..2], paramIndex);
        payload[2] = targetSystemId;
        payload[3] = targetComponentId;

        // Write parameter ID (null-padded, max 16 chars)
        WriteParamId(payload[4..20], paramId);

        return BuildV2Packet(
            255, // GCS
            190, // MAV_COMP_ID_MISSIONPLANNER
            MessageIds.ParamRequestRead,
            payload);
    }

    /// <inheritdoc />
    public byte[] EncodeParamSet(byte targetSystemId, byte targetComponentId, string paramId, float paramValue, MavParamType paramType)
    {
        // PARAM_SET payload layout (23 bytes):
        // float param_value     (4 bytes, offset 0)
        // uint8 target_system   (1 byte, offset 4)
        // uint8 target_component (1 byte, offset 5)
        // char[16] param_id     (16 bytes, offset 6)
        // uint8 param_type      (1 byte, offset 22)

        Span<byte> payload = stackalloc byte[23];

        BinaryPrimitives.WriteSingleLittleEndian(payload[0..4], paramValue);
        payload[4] = targetSystemId;
        payload[5] = targetComponentId;

        // Write parameter ID (null-padded, max 16 chars)
        WriteParamId(payload[6..22], paramId);

        payload[22] = (byte)paramType;

        return BuildV2Packet(
            255, // GCS
            190, // MAV_COMP_ID_MISSIONPLANNER
            MessageIds.ParamSet,
            payload);
    }

    private byte[] BuildV2Packet(byte systemId, byte componentId, uint messageId, ReadOnlySpan<byte> payload)
    {
        var packetLength = 10 + payload.Length + 2;
        var packet = new byte[packetLength];

        packet[0] = 0xFD; // MAVLink v2 magic byte
        packet[1] = (byte)payload.Length;
        packet[2] = 0x00; // Incompatibility flags
        packet[3] = 0x00; // Compatibility flags
        packet[4] = sequence++;
        packet[5] = systemId;
        packet[6] = componentId;
        packet[7] = (byte)(messageId & 0xFF);
        packet[8] = (byte)((messageId >> 8) & 0xFF);
        packet[9] = (byte)((messageId >> 16) & 0xFF);

        payload.CopyTo(packet.AsSpan(10));

        if (!crcExtraProvider.TryGetCrcExtra(messageId, out var crcExtra))
        {
            throw new InvalidOperationException($"No CRC extra registered for MAVLink message id {messageId}.");
        }

        var crc = MavLinkCrc.Calculate(
            packet.AsSpan(1, 9 + payload.Length),
            crcExtra);

        var crcOffset = 10 + payload.Length;
        packet[crcOffset] = (byte)(crc & 0xFF);
        packet[crcOffset + 1] = (byte)((crc >> 8) & 0xFF);

        return packet;
    }

    private static void WriteParamId(Span<byte> target, string paramId)
    {
        // Clear the span first (null padding)
        target.Clear();

        // Encode parameter ID as ASCII (max 16 characters)
        var maxLength = Math.Min(paramId.Length, 16);
        var bytesWritten = System.Text.Encoding.ASCII.GetBytes(paramId.AsSpan(0, maxLength), target);

        // Remaining bytes are already zeroed from Clear()
    }
}
