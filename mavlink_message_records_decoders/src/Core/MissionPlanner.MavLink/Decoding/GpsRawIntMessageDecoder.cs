using MissionPlanner.MavLink.Messages;
using MissionPlanner.MavLink.Services;

namespace MissionPlanner.MavLink.Decoding;

/// <summary>
/// Decodes MAVLink GPS_RAW_INT messages.
/// </summary>
public sealed class GpsRawIntMessageDecoder : IMavLinkMessageDecoder
{
    /// <inheritdoc />
    public bool TryDecode(MavLinkFrame frame, out MavLinkMessage? message)
    {
        message = null;

        if (frame.MessageId != MessageIds.GpsRawInt)
        {
            return false;
        }

        if (frame.Payload.Length < 30)
        {
            return false;
        }

        var span = frame.Payload.Span;

        var latRaw = MavLinkDecoderHelpers.ReadInt32OrDefault(span, 8);
        var lonRaw = MavLinkDecoderHelpers.ReadInt32OrDefault(span, 12);
        var altRaw = MavLinkDecoderHelpers.ReadInt32OrDefault(span, 16);

        double? yaw = null;
        if (frame.Payload.Length >= 52)
        {
            var yawRaw = MavLinkDecoderHelpers.ReadUInt16OrDefault(span, 50, ushort.MaxValue);
            yaw = yawRaw == ushort.MaxValue ? null : yawRaw / 100.0;
        }

        message = new GpsRawIntMessage(
            frame.SystemId,
            frame.ComponentId,
            frame.EndPoint,
            MavLinkDecoderHelpers.ReadUInt64OrDefault(span, 0),
            MavLinkDecoderHelpers.ReadByteOrDefault(span, 28),
            latRaw / 10_000_000.0,
            lonRaw / 10_000_000.0,
            altRaw / 1000.0,
            MavLinkDecoderHelpers.ReadUInt16OrDefault(span, 20, ushort.MaxValue),
            MavLinkDecoderHelpers.ReadUInt16OrDefault(span, 22, ushort.MaxValue),
            MavLinkDecoderHelpers.ReadUInt16OrDefault(span, 24, ushort.MaxValue),
            MavLinkDecoderHelpers.ReadUInt16OrDefault(span, 26, ushort.MaxValue),
            MavLinkDecoderHelpers.ReadByteOrDefault(span, 29, byte.MaxValue),
            frame.Payload.Length >= 34 ? MavLinkDecoderHelpers.ReadInt32OrDefault(span, 30) / 1000.0 : null,
            frame.Payload.Length >= 38 ? MavLinkDecoderHelpers.ReadUInt32OrDefault(span, 34) : null,
            frame.Payload.Length >= 42 ? MavLinkDecoderHelpers.ReadUInt32OrDefault(span, 38) : null,
            frame.Payload.Length >= 46 ? MavLinkDecoderHelpers.ReadUInt32OrDefault(span, 42) : null,
            frame.Payload.Length >= 50 ? MavLinkDecoderHelpers.ReadUInt32OrDefault(span, 46) : null,
            yaw,
            frame.ReceivedAt);

        return true;
    }
}
