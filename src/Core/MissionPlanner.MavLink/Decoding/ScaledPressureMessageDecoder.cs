using MissionPlanner.MavLink.Decoding.Utils;
using MissionPlanner.MavLink.Messages;
using MissionPlanner.MavLink.Services;

namespace MissionPlanner.MavLink.Decoding;

/// <summary>
/// Decodes MAVLink SCALED_PRESSURE messages.
/// </summary>
public sealed class ScaledPressureMessageDecoder : IMavLinkMessageDecoder
{
    /// <inheritdoc />
    public uint MessageId { get; } = MessageIds.ScaledPressure;

    /// <inheritdoc />
    public byte CrcExtra { get; } = 115;

    /// <inheritdoc />
    public bool TryDecode(MavLinkFrame frame, out MavLinkMessage? message)
    {
        message = null;

        if (frame.MessageId != MessageId)
        {
            return false;
        }

        if (frame.Payload.Length < 14)
        {
            return false;
        }

        var span = frame.Payload.Span;

        message = new ScaledPressureMessage(
            frame.SystemId,
            frame.ComponentId,
            frame.EndPoint,
            MavLinkDecoderHelpers.ReadUInt32OrDefault(span, 0),
            MavLinkDecoderHelpers.ReadSingleOrDefault(span, 4),
            MavLinkDecoderHelpers.ReadSingleOrDefault(span, 8),
            MavLinkDecoderHelpers.ReadInt16OrDefault(span, 12),
            frame.Payload.Length >= 16 ? MavLinkDecoderHelpers.ReadInt16OrDefault(span, 14) : null,
            frame.ReceivedAt);

        return true;
    }
}
