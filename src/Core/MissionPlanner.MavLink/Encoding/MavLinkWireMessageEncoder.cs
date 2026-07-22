using MissionPlanner.MavLink.Messages;
using MissionPlanner.MavLink.Services.Abstractions;

namespace MissionPlanner.MavLink.Encoding;

/// <summary>Encodes generated dialect-backed messages as unsigned MAVLink 2 wire packets.</summary>
public sealed class MavLinkWireMessageEncoder(IMavLinkCrcExtraProvider crcExtraProvider) : IMavLinkWireMessageEncoder
{
    private int sequence;

    /// <inheritdoc />
    public byte[] Encode(GeneratedMavLinkMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (message.MessageId > 0x00ff_ffff)
        {
            throw new ArgumentOutOfRangeException(nameof(message), "MAVLink 2 message IDs use 24 bits.");
        }

        var payload = message.EncodePayload();
        var packet = new byte[10 + payload.Length + 2];
        packet[0] = 0xfd;
        packet[1] = checked((byte)payload.Length);
        packet[4] = unchecked((byte)Interlocked.Increment(ref sequence));
        packet[5] = message.SystemId;
        packet[6] = message.ComponentId;
        packet[7] = (byte)message.MessageId;
        packet[8] = (byte)(message.MessageId >> 8);
        packet[9] = (byte)(message.MessageId >> 16);
        payload.CopyTo(packet.AsSpan(10));

        if (!crcExtraProvider.TryGetCrcExtra(message.MessageId, out var crcExtra))
        {
            throw new InvalidOperationException($"MAVLink CRC extra is not registered for message {message.MessageId}.");
        }

        var crc = MavLinkCrc.Calculate(packet.AsSpan(1, 9 + payload.Length), crcExtra);
        packet[^2] = (byte)crc;
        packet[^1] = (byte)(crc >> 8);
        return packet;
    }
}
