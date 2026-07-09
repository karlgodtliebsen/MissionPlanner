using MissionPlanner.MavLink.Messages;
using MissionPlanner.MavLink.Services;

namespace MissionPlanner.MavLink.Decoding;

/// <summary>
/// Decodes MAVLink RC_CHANNELS messages.
/// </summary>
public sealed class RcChannelsMessageDecoder : IMavLinkMessageDecoder
{
    /// <inheritdoc />
    public bool TryDecode(MavLinkFrame frame, out MavLinkMessage? message)
    {
        message = null;

        if (frame.MessageId != MessageIds.RcChannels)
        {
            return false;
        }

        if (frame.Payload.Length < 42)
        {
            return false;
        }

        var span = frame.Payload.Span;
        var channels = new ushort[18];

        for (var i = 0; i < channels.Length; i++)
        {
            channels[i] = MavLinkDecoderHelpers.ReadUInt16OrDefault(span, 4 + i * 2, ushort.MaxValue);
        }

        message = new RcChannelsMessage(
            frame.SystemId,
            frame.ComponentId,
            frame.EndPoint,
            MavLinkDecoderHelpers.ReadUInt32OrDefault(span, 0),
            MavLinkDecoderHelpers.ReadByteOrDefault(span, 40),
            channels,
            MavLinkDecoderHelpers.ReadByteOrDefault(span, 41, byte.MaxValue),
            frame.ReceivedAt);

        return true;
    }
}
