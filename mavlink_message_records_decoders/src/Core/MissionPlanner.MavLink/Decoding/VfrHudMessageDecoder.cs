using MissionPlanner.MavLink.Messages;
using MissionPlanner.MavLink.Services;

namespace MissionPlanner.MavLink.Decoding;

/// <summary>
/// Decodes MAVLink VFR_HUD messages.
/// </summary>
public sealed class VfrHudMessageDecoder : IMavLinkMessageDecoder
{
    /// <inheritdoc />
    public bool TryDecode(MavLinkFrame frame, out MavLinkMessage? message)
    {
        message = null;

        if (frame.MessageId != MessageIds.VfrHud)
        {
            return false;
        }

        if (frame.Payload.Length < 20)
        {
            return false;
        }

        var span = frame.Payload.Span;

        message = new VfrHudMessage(
            frame.SystemId,
            frame.ComponentId,
            frame.EndPoint,
            MavLinkDecoderHelpers.ReadSingleOrDefault(span, 0),
            MavLinkDecoderHelpers.ReadSingleOrDefault(span, 4),
            MavLinkDecoderHelpers.ReadInt16OrDefault(span, 16),
            MavLinkDecoderHelpers.ReadUInt16OrDefault(span, 18),
            MavLinkDecoderHelpers.ReadSingleOrDefault(span, 8),
            MavLinkDecoderHelpers.ReadSingleOrDefault(span, 12),
            frame.ReceivedAt);

        return true;
    }
}
