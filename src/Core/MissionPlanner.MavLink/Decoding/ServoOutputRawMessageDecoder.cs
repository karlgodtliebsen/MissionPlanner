using MissionPlanner.MavLink.Decoding.Utils;
using MissionPlanner.MavLink.Messages;
using MissionPlanner.MavLink.Services;

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

        if (frame.Payload.Length < 21)
        {
            return false;
        }

        var span = frame.Payload.Span;
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
