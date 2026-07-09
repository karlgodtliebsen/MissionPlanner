using MissionPlanner.MavLink.Messages;
using MissionPlanner.MavLink.Services;

namespace MissionPlanner.MavLink.Decoding;

/// <summary>
/// Decodes ArduPilot MAVLink AHRS2 messages.
/// </summary>
public sealed class Ahrs2MessageDecoder : IMavLinkMessageDecoder
{
    /// <inheritdoc />
    public bool TryDecode(MavLinkFrame frame, out MavLinkMessage? message)
    {
        message = null;

        if (frame.MessageId != MessageIds.Ahrs2)
        {
            return false;
        }

        if (frame.Payload.Length < 24)
        {
            return false;
        }

        var span = frame.Payload.Span;

        message = new Ahrs2Message(
            frame.SystemId,
            frame.ComponentId,
            frame.EndPoint,
            MavLinkDecoderHelpers.ReadSingleOrDefault(span, 0),
            MavLinkDecoderHelpers.ReadSingleOrDefault(span, 4),
            MavLinkDecoderHelpers.ReadSingleOrDefault(span, 8),
            MavLinkDecoderHelpers.ReadSingleOrDefault(span, 12),
            MavLinkDecoderHelpers.ReadInt32OrDefault(span, 16) / 10_000_000.0,
            MavLinkDecoderHelpers.ReadInt32OrDefault(span, 20) / 10_000_000.0,
            frame.ReceivedAt);

        return true;
    }
}
