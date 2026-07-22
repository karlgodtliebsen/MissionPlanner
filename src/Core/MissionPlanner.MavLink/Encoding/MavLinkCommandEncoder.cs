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
    public byte[] EncodeCommandLong(byte targetSystemId, byte targetComponentId, ushort commandId, IReadOnlyList<float> parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        if (parameters.Count > 7)
        {
            throw new ArgumentException("COMMAND_LONG supports at most seven parameters.", nameof(parameters));
        }

        Span<byte> payload = stackalloc byte[33];
        for (var index = 0; index < 7; index++)
        {
            var value = index < parameters.Count ? parameters[index] : 0;
            if (!float.IsFinite(value))
            {
                throw new ArgumentException("COMMAND_LONG parameters must be finite.", nameof(parameters));
            }

            WriteFloat(payload.Slice(index * 4, 4), value);
        }

        BinaryPrimitives.WriteUInt16LittleEndian(payload[28..30], commandId);
        payload[30] = targetSystemId;
        payload[31] = targetComponentId;
        payload[32] = 0;
        return BuildV2Packet(255, 190, MessageIds.CommandLong, payload);
    }

    /// <inheritdoc />
    public byte[] EncodeArmDisarm(byte targetSystemId, byte targetComponentId, bool arm)
    {
        return EncodeCommandLong(targetSystemId, targetComponentId, MavLinkCommandIds.ComponentArmDisarm, [arm ? 1.0f : 0.0f]);
    }


    /// <inheritdoc />
    public byte[] EncodeSetMode(byte targetSystemId, byte targetComponentId, uint customMode)
    {
        return EncodeCommandLong(targetSystemId, targetComponentId, MavLinkCommandIds.DoSetMode, [1.0f, customMode]);
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
