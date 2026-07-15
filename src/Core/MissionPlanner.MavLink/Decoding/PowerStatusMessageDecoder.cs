using MissionPlanner.MavLink.Decoding.Utils;
using MissionPlanner.MavLink.Messages;
using MissionPlanner.MavLink.Services.Abstractions;

namespace MissionPlanner.MavLink.Decoding;

/// <summary>
/// Decodes MAVLink POWER_STATUS messages.
/// </summary>
public sealed class PowerStatusMessageDecoder : IMavLinkMessageDecoder
{
    /// <inheritdoc />
    public uint MessageId { get; } = MessageIds.PowerStatus;

    /// <inheritdoc />
    public byte CrcExtra { get; } = 203;

    /// <inheritdoc />
    public bool TryDecode(MavLinkFrame frame, out MavLinkMessage? message)
    {
        message = null;

        if (frame.MessageId != MessageId)
        {
            return false;
        }

        if (frame.Payload.Length < 6)
        {
            return false;
        }

        var span = frame.Payload.Span;

        message = new PowerStatusMessage(
            frame.SystemId,
            frame.ComponentId,
            frame.EndPoint,
            MavLinkDecoderHelpers.ReadUInt16OrDefault(span, 0),
            MavLinkDecoderHelpers.ReadUInt16OrDefault(span, 2),
            MavLinkDecoderHelpers.ReadUInt16OrDefault(span, 4),
            frame.ReceivedAt);

        return true;
    }
}
