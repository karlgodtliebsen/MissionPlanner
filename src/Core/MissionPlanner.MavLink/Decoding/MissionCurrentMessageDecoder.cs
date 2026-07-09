using MissionPlanner.MavLink.Decoding.Utils;
using MissionPlanner.MavLink.Messages;
using MissionPlanner.MavLink.Services;

namespace MissionPlanner.MavLink.Decoding;

/// <summary>
/// Decodes MAVLink MISSION_CURRENT messages.
/// </summary>
public sealed class MissionCurrentMessageDecoder : IMavLinkMessageDecoder
{
    /// <inheritdoc />
    public uint MessageId { get; } = MessageIds.MissionCurrent;

    /// <inheritdoc />
    public byte CrcExtra { get; } = 28;

    /// <inheritdoc />
    public bool TryDecode(MavLinkFrame frame, out MavLinkMessage? message)
    {
        message = null;

        if (frame.MessageId != MessageId)
        {
            return false;
        }

        if (frame.Payload.Length < 2)
        {
            return false;
        }

        var span = frame.Payload.Span;

        message = new MissionCurrentMessage(
            frame.SystemId,
            frame.ComponentId,
            frame.EndPoint,
            MavLinkDecoderHelpers.ReadUInt16OrDefault(span, 0),
            frame.Payload.Length >= 4 ? MavLinkDecoderHelpers.ReadUInt16OrDefault(span, 2) : null,
            frame.Payload.Length >= 5 ? MavLinkDecoderHelpers.ReadByteOrDefault(span, 4) : null,
            frame.Payload.Length >= 6 ? MavLinkDecoderHelpers.ReadByteOrDefault(span, 5) : null,
            frame.ReceivedAt);

        return true;
    }
}
