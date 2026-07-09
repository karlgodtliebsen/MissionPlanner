using MissionPlanner.MavLink.Decoding.Utils;
using MissionPlanner.MavLink.Messages;
using MissionPlanner.MavLink.Services;

namespace MissionPlanner.MavLink.Decoding;

/// <summary>
/// Decodes MAVLink NAV_CONTROLLER_OUTPUT messages.
/// </summary>
public sealed class NavControllerOutputMessageDecoder : IMavLinkMessageDecoder
{
    /// <inheritdoc />
    public uint MessageId { get; } = MessageIds.NavControllerOutput;

    /// <inheritdoc />
    public byte CrcExtra { get; } = 183;

    /// <inheritdoc />
    public bool TryDecode(MavLinkFrame frame, out MavLinkMessage? message)
    {
        message = null;

        if (frame.MessageId != MessageId)
        {
            return false;
        }

        if (frame.Payload.Length < 26)
        {
            return false;
        }

        var span = frame.Payload.Span;

        message = new NavControllerOutputMessage(
            frame.SystemId,
            frame.ComponentId,
            frame.EndPoint,
            MavLinkDecoderHelpers.ReadSingleOrDefault(span, 0),
            MavLinkDecoderHelpers.ReadSingleOrDefault(span, 4),
            MavLinkDecoderHelpers.ReadInt16OrDefault(span, 20),
            MavLinkDecoderHelpers.ReadInt16OrDefault(span, 22),
            MavLinkDecoderHelpers.ReadUInt16OrDefault(span, 24),
            MavLinkDecoderHelpers.ReadSingleOrDefault(span, 8),
            MavLinkDecoderHelpers.ReadSingleOrDefault(span, 12),
            MavLinkDecoderHelpers.ReadSingleOrDefault(span, 16),
            frame.ReceivedAt);

        return true;
    }
}
