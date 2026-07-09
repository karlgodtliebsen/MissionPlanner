using System.Buffers.Binary;
using MissionPlanner.MavLink.Messages;
using MissionPlanner.MavLink.Services;

namespace MissionPlanner.MavLink.Decoding;

/// <inheritdoc />
public sealed class AttitudeMessageDecoder : IMavLinkMessageDecoder
{
    /// <inheritdoc />
    public uint MessageId { get; } = MessageIds.Attitude;

    /// <inheritdoc />
    public byte CrcExtra { get; } = 39;


    /// <inheritdoc />
    public bool TryDecode(MavLinkFrame frame, out MavLinkMessage? message)
    {
        message = null;

        if (frame.MessageId != MessageIds.Attitude)
        {
            return false;
        }

        // MAVLink v2 uses payload truncation (removes trailing zeros)
        // Minimum required: time_boot_ms(4) + roll(4) + pitch(4) + yaw(4) = 16 bytes
        if (frame.Payload.Length < 16)
        {
            return false;
        }

        var span = frame.Payload.Span;

        message = new AttitudeMessage(
            frame.SystemId,
            frame.ComponentId,
            frame.EndPoint,
            ReadSingle(span[4..8]),
            ReadSingle(span[8..12]),
            ReadSingle(span[12..16]),
            frame.ReceivedAt);

        return true;
    }

    private static float ReadSingle(ReadOnlySpan<byte> value)
    {
        var raw = BinaryPrimitives.ReadInt32LittleEndian(value);
        return BitConverter.Int32BitsToSingle(raw);
    }
}
