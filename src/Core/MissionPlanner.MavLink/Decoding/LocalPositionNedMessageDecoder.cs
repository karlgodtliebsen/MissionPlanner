using MissionPlanner.MavLink.Decoding.Utils;
using MissionPlanner.MavLink.Messages;
using MissionPlanner.MavLink.Services;

namespace MissionPlanner.MavLink.Decoding;

/// <summary>
/// Decodes MAVLink LOCAL_POSITION_NED messages.
/// </summary>
public sealed class LocalPositionNedMessageDecoder : IMavLinkMessageDecoder
{
    /// <inheritdoc />
    public uint MessageId { get; } = MessageIds.LocalPositionNed;

    /// <inheritdoc />
    public byte CrcExtra { get; } = 185;

    /// <inheritdoc />
    public bool TryDecode(MavLinkFrame frame, out MavLinkMessage? message)
    {
        message = null;

        if (frame.MessageId != MessageId)
        {
            return false;
        }

        if (frame.Payload.Length < 28)
        {
            return false;
        }

        var span = frame.Payload.Span;

        message = new LocalPositionNedMessage(
            frame.SystemId,
            frame.ComponentId,
            frame.EndPoint,
            MavLinkDecoderHelpers.ReadUInt32OrDefault(span, 0),
            MavLinkDecoderHelpers.ReadSingleOrDefault(span, 4),
            MavLinkDecoderHelpers.ReadSingleOrDefault(span, 8),
            MavLinkDecoderHelpers.ReadSingleOrDefault(span, 12),
            MavLinkDecoderHelpers.ReadSingleOrDefault(span, 16),
            MavLinkDecoderHelpers.ReadSingleOrDefault(span, 20),
            MavLinkDecoderHelpers.ReadSingleOrDefault(span, 24),
            frame.ReceivedAt);

        return true;
    }
}
