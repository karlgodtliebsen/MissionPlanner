using MissionPlanner.MavLink.Decoding.Utils;
using MissionPlanner.MavLink.Messages;
using MissionPlanner.MavLink.Services.Abstractions;

namespace MissionPlanner.MavLink.Decoding;

/// <summary>
/// Decodes MAVLink BATTERY_STATUS messages.
/// </summary>
public sealed class BatteryStatusMessageDecoder : IMavLinkMessageDecoder
{
    /// <inheritdoc />
    public uint MessageId { get; } = MessageIds.BatteryStatus;

    /// <inheritdoc />
    public byte CrcExtra { get; } = 154;

    /// <inheritdoc />
    public bool TryDecode(MavLinkFrame frame, out MavLinkMessage? message)
    {
        message = null;

        if (frame.MessageId != MessageId)
        {
            return false;
        }

        if (frame.Payload.Length == 0)
        {
            return false;
        }

        // MAVLink 2 removes trailing zero bytes, including partially present scalar fields.
        Span<byte> span = stackalloc byte[54];
        span.Clear();
        frame.Payload.Span[..Math.Min(frame.Payload.Length, span.Length)].CopyTo(span);
        var voltages = new ushort[10];
        for (var i = 0; i < voltages.Length; i++)
        {
            voltages[i] = MavLinkDecoderHelpers.ReadUInt16OrDefault(span, 10 + i * 2, ushort.MaxValue);
        }

        var voltagesExt = new ushort[4];
        for (var i = 0; i < voltagesExt.Length; i++)
        {
            voltagesExt[i] = MavLinkDecoderHelpers.ReadUInt16OrDefault(span, 41 + i * 2, 0);
        }

        message = new BatteryStatusMessage(
            frame.SystemId,
            frame.ComponentId,
            frame.EndPoint,
            MavLinkDecoderHelpers.ReadByteOrDefault(span, 32),
            MavLinkDecoderHelpers.ReadByteOrDefault(span, 33),
            MavLinkDecoderHelpers.ReadByteOrDefault(span, 34),
            MavLinkDecoderHelpers.ReadInt16OrDefault(span, 8),
            voltages,
            MavLinkDecoderHelpers.ReadInt16OrDefault(span, 30),
            MavLinkDecoderHelpers.ReadInt32OrDefault(span, 0),
            MavLinkDecoderHelpers.ReadInt32OrDefault(span, 4),
            MavLinkDecoderHelpers.ReadSByteOrDefault(span, 35, -1),
            frame.Payload.Length > 36 ? MavLinkDecoderHelpers.ReadInt32OrDefault(span, 36) : null,
            frame.Payload.Length >= 41 ? MavLinkDecoderHelpers.ReadByteOrDefault(span, 40) : null,
            voltagesExt,
            frame.Payload.Length >= 50 ? MavLinkDecoderHelpers.ReadByteOrDefault(span, 49) : null,
            frame.Payload.Length >= 51 ? MavLinkDecoderHelpers.ReadUInt32OrDefault(span, 50) : null,
            frame.ReceivedAt);

        return true;
    }
}
