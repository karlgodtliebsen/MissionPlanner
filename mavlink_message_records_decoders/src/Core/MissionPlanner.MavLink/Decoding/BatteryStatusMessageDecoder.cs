using MissionPlanner.MavLink.Messages;
using MissionPlanner.MavLink.Services;

namespace MissionPlanner.MavLink.Decoding;

/// <summary>
/// Decodes MAVLink BATTERY_STATUS messages.
/// </summary>
public sealed class BatteryStatusMessageDecoder : IMavLinkMessageDecoder
{
    /// <inheritdoc />
    public bool TryDecode(MavLinkFrame frame, out MavLinkMessage? message)
    {
        message = null;

        if (frame.MessageId != MessageIds.BatteryStatus)
        {
            return false;
        }

        if (frame.Payload.Length < 36)
        {
            return false;
        }

        var span = frame.Payload.Span;
        var voltages = new ushort[10];
        for (var i = 0; i < voltages.Length; i++)
        {
            voltages[i] = MavLinkDecoderHelpers.ReadUInt16OrDefault(span, 10 + i * 2, ushort.MaxValue);
        }

        var voltagesExt = new ushort[4];
        for (var i = 0; i < voltagesExt.Length; i++)
        {
            voltagesExt[i] = MavLinkDecoderHelpers.ReadUInt16OrDefault(span, 42 + i * 2, ushort.MaxValue);
        }

        message = new BatteryStatusMessage(
            frame.SystemId,
            frame.ComponentId,
            frame.EndPoint,
            MavLinkDecoderHelpers.ReadByteOrDefault(span, 30),
            MavLinkDecoderHelpers.ReadByteOrDefault(span, 31),
            MavLinkDecoderHelpers.ReadByteOrDefault(span, 32),
            MavLinkDecoderHelpers.ReadInt16OrDefault(span, 8),
            voltages,
            MavLinkDecoderHelpers.ReadInt16OrDefault(span, 34),
            MavLinkDecoderHelpers.ReadInt32OrDefault(span, 0),
            MavLinkDecoderHelpers.ReadInt32OrDefault(span, 4),
            MavLinkDecoderHelpers.ReadSByteOrDefault(span, 35, -1),
            frame.Payload.Length >= 40 ? MavLinkDecoderHelpers.ReadInt32OrDefault(span, 36) : null,
            frame.Payload.Length >= 41 ? MavLinkDecoderHelpers.ReadByteOrDefault(span, 40) : null,
            voltagesExt,
            frame.Payload.Length >= 51 ? MavLinkDecoderHelpers.ReadByteOrDefault(span, 50) : null,
            frame.Payload.Length >= 55 ? MavLinkDecoderHelpers.ReadUInt32OrDefault(span, 51) : null,
            frame.ReceivedAt);

        return true;
    }
}
