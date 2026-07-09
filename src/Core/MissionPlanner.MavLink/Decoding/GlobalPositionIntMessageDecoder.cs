using System.Buffers.Binary;
using MissionPlanner.MavLink.Messages;
using MissionPlanner.MavLink.Services;

namespace MissionPlanner.MavLink.Decoding;

/// <inheritdoc />
public sealed class GlobalPositionIntMessageDecoder : IMavLinkMessageDecoder
{
    /// <inheritdoc />
    public uint MessageId { get; } = MessageIds.GlobalPositionInt;

    /// <inheritdoc />
    public byte CrcExtra { get; } = 104;

    /// <inheritdoc />
    public bool TryDecode(MavLinkFrame frame, out MavLinkMessage? message)
    {
        message = null;

        if (frame.MessageId != MessageIds.GlobalPositionInt)
        {
            return false;
        }

        // MAVLink v2 uses payload truncation (removes trailing zeros)
        // Minimum required: time_boot_ms(4) + lat(4) + lon(4) + alt(4) = 16 bytes
        if (frame.Payload.Length < 16)
        {
            return false;
        }

        var span = frame.Payload.Span;

        var latRaw = BinaryPrimitives.ReadInt32LittleEndian(span[4..8]);
        var lonRaw = BinaryPrimitives.ReadInt32LittleEndian(span[8..12]);
        var altRaw = BinaryPrimitives.ReadInt32LittleEndian(span[12..16]);

        message = new GlobalPositionIntMessage(
            frame.SystemId,
            frame.ComponentId,
            frame.EndPoint,
            latRaw / 10_000_000.0,
            lonRaw / 10_000_000.0,
            altRaw / 1000.0,
            frame.ReceivedAt);

        return true;
    }
}
