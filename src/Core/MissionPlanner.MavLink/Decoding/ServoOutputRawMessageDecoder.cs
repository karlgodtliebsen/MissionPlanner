using MissionPlanner.MavLink.Decoding.Utils;
using MissionPlanner.MavLink.Messages;
using MissionPlanner.MavLink.Services.Abstractions;

namespace MissionPlanner.MavLink.Decoding;

/// <summary>
/// Decodes MAVLink SERVO_OUTPUT_RAW messages.
/// </summary>
public sealed class ServoOutputRawMessageDecoder : IMavLinkMessageDecoder
{
    /// <inheritdoc />
    public uint MessageId { get; } = MessageIds.ServoOutputRaw;

    /// <inheritdoc />
    public byte CrcExtra { get; } = 222;

    /// <inheritdoc />
    public bool TryDecode(MavLinkFrame frame, out MavLinkMessage? message)
    {
        message = null;

        if (frame.MessageId != MessageId)
        {
            return false;
        }

        if (frame.Payload.Length is < 1 or > 37)
        {
            return false;
        }

        // The parser validates wire lengths. MAVLink 2 can trim zeros even within
        // base fields or halfway through a ushort, so pad before reading values.
        Span<byte> span = stackalloc byte[37];
        span.Clear();
        frame.Payload.Span.CopyTo(span);
        var servos = new ushort[16];

        for (var i = 0; i < 8; i++)
        {
            servos[i] = MavLinkDecoderHelpers.ReadUInt16OrDefault(span, 4 + i * 2);
        }

        for (var i = 8; i < 16; i++)
        {
            servos[i] = MavLinkDecoderHelpers.ReadUInt16OrDefault(span, 21 + (i - 8) * 2);
        }

        message = new ServoOutputRawMessage(
            frame.SystemId,
            frame.ComponentId,
            frame.EndPoint,
            MavLinkDecoderHelpers.ReadUInt32OrDefault(span, 0),
            MavLinkDecoderHelpers.ReadByteOrDefault(span, 20),
            servos,
            frame.ReceivedAt);

        return true;
    }
}
