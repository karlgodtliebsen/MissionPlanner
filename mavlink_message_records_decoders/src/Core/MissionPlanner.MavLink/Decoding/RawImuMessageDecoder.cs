using MissionPlanner.MavLink.Messages;
using MissionPlanner.MavLink.Services;

namespace MissionPlanner.MavLink.Decoding;

/// <summary>
/// Decodes MAVLink RAW_IMU messages.
/// </summary>
public sealed class RawImuMessageDecoder : IMavLinkMessageDecoder
{
    /// <inheritdoc />
    public bool TryDecode(MavLinkFrame frame, out MavLinkMessage? message)
    {
        message = null;

        if (frame.MessageId != MessageIds.RawImu)
        {
            return false;
        }

        if (frame.Payload.Length < 26)
        {
            return false;
        }

        var span = frame.Payload.Span;

        message = new RawImuMessage(
            frame.SystemId,
            frame.ComponentId,
            frame.EndPoint,
            MavLinkDecoderHelpers.ReadUInt64OrDefault(span, 0),
            MavLinkDecoderHelpers.ReadInt16OrDefault(span, 8),
            MavLinkDecoderHelpers.ReadInt16OrDefault(span, 10),
            MavLinkDecoderHelpers.ReadInt16OrDefault(span, 12),
            MavLinkDecoderHelpers.ReadInt16OrDefault(span, 14),
            MavLinkDecoderHelpers.ReadInt16OrDefault(span, 16),
            MavLinkDecoderHelpers.ReadInt16OrDefault(span, 18),
            MavLinkDecoderHelpers.ReadInt16OrDefault(span, 20),
            MavLinkDecoderHelpers.ReadInt16OrDefault(span, 22),
            MavLinkDecoderHelpers.ReadInt16OrDefault(span, 24),
            frame.Payload.Length >= 27 ? MavLinkDecoderHelpers.ReadByteOrDefault(span, 26) : null,
            frame.Payload.Length >= 29 ? MavLinkDecoderHelpers.ReadInt16OrDefault(span, 27) : null,
            frame.ReceivedAt);

        return true;
    }
}
