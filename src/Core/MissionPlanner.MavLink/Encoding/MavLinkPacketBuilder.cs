using System.Buffers.Binary;

namespace MissionPlanner.MavLink.Encoding;

/// <summary>
/// Builds MAVLink v1 packets for sending commands to vehicles.
/// Handles packet structure, payload encoding, and CRC-16/X.25 checksum calculation.
/// </summary>
public static class MavLinkPacketBuilder
{
    // MAVLink v1 packet constants
    private const byte MavLinkV1StartByte = 0xFE;
    private const int HeaderLength = 6;
    private const int CrcLength = 2;

    private static readonly Services.Abstractions.IMavLinkMessageDefinitionRegistry MessageDefinitions =
        new Services.MavLinkMessageDefinitionRegistry();

    /// <summary>
    /// Builds a REQUEST_DATA_STREAM packet to request telemetry streams from ArduPilot.
    /// </summary>
    /// <param name="targetSystem">Target system ID (usually 1)</param>
    /// <param name="targetComponent">Target component ID (usually 1 for autopilot)</param>
    /// <param name="streamId">Stream ID to request (e.g., MAV_DATA_STREAM_EXTRA1 = 10)</param>
    /// <param name="messageRate">Requested rate in Hz</param>
    /// <param name="startStop">1 to start streaming, 0 to stop</param>
    /// <param name="sequenceNumber">Packet sequence number for tracking</param>
    /// <returns>Complete MAVLink v1 packet ready to send</returns>
    public static byte[] BuildRequestDataStreamPacket(
        byte targetSystem,
        byte targetComponent,
        byte streamId,
        ushort messageRate,
        byte startStop,
        byte sequenceNumber = 0)
    {
        const byte MESSAGE_ID = 66; // REQUEST_DATA_STREAM
        const byte PAYLOAD_LENGTH = 6;

        // Packet structure:
        // [0]    : Start byte (0xFE)
        // [1]    : Payload length
        // [2]    : Sequence
        // [3]    : System ID (sender, GCS = 255)
        // [4]    : Component ID (sender, GCS = 0 or 190)
        // [5]    : Message ID
        // [6-11] : Payload (6 bytes)
        // [12-13]: CRC16

        byte[] packet = new byte[HeaderLength + PAYLOAD_LENGTH + CrcLength];

        // Header
        packet[0] = MavLinkV1StartByte;
        packet[1] = PAYLOAD_LENGTH;
        packet[2] = sequenceNumber;
        packet[3] = 255; // GCS system ID
        packet[4] = 190; // GCS component ID (MAV_COMP_ID_MISSIONPLANNER)
        packet[5] = MESSAGE_ID;

        // Payload: REQUEST_DATA_STREAM
        // Field layout (little-endian):
        // - message_rate (uint16) : bytes 0-1
        // - target_system (uint8) : byte 2
        // - target_component (uint8) : byte 3
        // - req_stream_id (uint8) : byte 4
        // - start_stop (uint8) : byte 5

        int payloadOffset = HeaderLength;
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(payloadOffset), messageRate);
        packet[payloadOffset + 2] = targetSystem;
        packet[payloadOffset + 3] = targetComponent;
        packet[payloadOffset + 4] = streamId;
        packet[payloadOffset + 5] = startStop;

        // Calculate CRC
        ushort crc = CalculateCrc(packet.AsSpan(1, HeaderLength - 1 + PAYLOAD_LENGTH), MESSAGE_ID);
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(HeaderLength + PAYLOAD_LENGTH), crc);

        return packet;
    }

    /// <summary>
    /// Builds a COMMAND_LONG packet (MAVLink v1) carrying a MAV_CMD with up to seven parameters.
    /// </summary>
    /// <param name="targetSystem">Target system ID (usually 1)</param>
    /// <param name="targetComponent">Target component ID (usually 1 for autopilot)</param>
    /// <param name="command">MAV_CMD command id</param>
    /// <param name="param1">Command parameter 1</param>
    /// <param name="param2">Command parameter 2</param>
    /// <param name="param3">Command parameter 3</param>
    /// <param name="param4">Command parameter 4</param>
    /// <param name="param5">Command parameter 5</param>
    /// <param name="param6">Command parameter 6</param>
    /// <param name="param7">Command parameter 7</param>
    /// <param name="sequenceNumber">Packet sequence number for tracking</param>
    /// <returns>Complete MAVLink v1 packet ready to send</returns>
    public static byte[] BuildCommandLongPacket(
        byte targetSystem,
        byte targetComponent,
        ushort command,
        float param1 = 0,
        float param2 = 0,
        float param3 = 0,
        float param4 = 0,
        float param5 = 0,
        float param6 = 0,
        float param7 = 0,
        byte sequenceNumber = 0)
    {
        const byte MESSAGE_ID = 76; // COMMAND_LONG
        const byte PAYLOAD_LENGTH = 33;

        byte[] packet = new byte[HeaderLength + PAYLOAD_LENGTH + CrcLength];

        packet[0] = MavLinkV1StartByte;
        packet[1] = PAYLOAD_LENGTH;
        packet[2] = sequenceNumber;
        packet[3] = 255; // GCS system ID
        packet[4] = 190; // GCS component ID (MAV_COMP_ID_MISSIONPLANNER)
        packet[5] = MESSAGE_ID;

        // Payload: COMMAND_LONG
        // - param1..param7 (float) : bytes 0-27
        // - command (uint16)       : bytes 28-29
        // - target_system (uint8)  : byte 30
        // - target_component (uint8): byte 31
        // - confirmation (uint8)   : byte 32

        int payloadOffset = HeaderLength;
        Span<float> parameters = [param1, param2, param3, param4, param5, param6, param7];
        for (var i = 0; i < parameters.Length; i++)
        {
            BinaryPrimitives.WriteSingleLittleEndian(packet.AsSpan(payloadOffset + i * 4), parameters[i]);
        }

        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(payloadOffset + 28), command);
        packet[payloadOffset + 30] = targetSystem;
        packet[payloadOffset + 31] = targetComponent;
        packet[payloadOffset + 32] = 0; // confirmation

        ushort crc = CalculateCrc(packet.AsSpan(1, HeaderLength - 1 + PAYLOAD_LENGTH), MESSAGE_ID);
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(HeaderLength + PAYLOAD_LENGTH), crc);

        return packet;
    }

    /// <summary>
    /// Calculates MAVLink CRC-16/X.25 checksum with CRC_EXTRA seed.
    /// </summary>
    /// <param name="data">Packet data (length + seq + sysid + compid + msgid + payload)</param>
    /// <param name="messageId">MAVLink message ID for CRC_EXTRA seed lookup</param>
    /// <returns>16-bit CRC checksum</returns>
    private static ushort CalculateCrc(ReadOnlySpan<byte> data, byte messageId)
    {
        if (!MessageDefinitions.TryGet(messageId, out var definition))
        {
            throw new InvalidOperationException($"MAVLink message definition not found for message ID {messageId}.");
        }

        return MavLinkCrc.Calculate(data, definition.CrcExtra);
    }
}
