using MissionPlanner.MavLink.Messages;
using MissionPlanner.MavLink.Services;

namespace MissionPlanner.MavLink.Decoding;

/// <summary>
/// Decodes MAVLink TIMESYNC messages.
/// </summary>
public sealed class TimeSyncMessageDecoder : IMavLinkMessageDecoder
{
    /// <inheritdoc />
    public bool TryDecode(MavLinkFrame frame, out MavLinkMessage? message)
    {
        message = null;

        if (frame.MessageId != MessageIds.TimeSync)
        {
            return false;
        }

        if (frame.Payload.Length < 16)
        {
            return false;
        }

        var span = frame.Payload.Span;

        message = new TimeSyncMessage(
            frame.SystemId,
            frame.ComponentId,
            frame.EndPoint,
            MavLinkDecoderHelpers.ReadInt64OrDefault(span, 0),
            MavLinkDecoderHelpers.ReadInt64OrDefault(span, 8),
            frame.ReceivedAt);

        return true;
    }
}
