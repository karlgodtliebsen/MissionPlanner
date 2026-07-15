using MissionPlanner.MavLink.Decoding.Utils;
using MissionPlanner.MavLink.Messages;
using MissionPlanner.MavLink.Services.Abstractions;

namespace MissionPlanner.MavLink.Decoding;

/// <summary>
/// Decodes ArduPilot MAVLink MEMINFO messages.
/// </summary>
public sealed class MemInfoMessageDecoder : IMavLinkMessageDecoder
{
    /// <inheritdoc />
    public uint MessageId { get; } = MessageIds.MemInfo;

    /// <inheritdoc />
    public byte CrcExtra { get; }

    /// <inheritdoc />
    public bool TryDecode(MavLinkFrame frame, out MavLinkMessage? message)
    {
        message = null;

        if (frame.MessageId != MessageId)
        {
            return false;
        }

        if (frame.Payload.Length < 4)
        {
            return false;
        }

        var span = frame.Payload.Span;

        message = new MemInfoMessage(
            frame.SystemId,
            frame.ComponentId,
            frame.EndPoint,
            MavLinkDecoderHelpers.ReadUInt16OrDefault(span, 0),
            MavLinkDecoderHelpers.ReadUInt16OrDefault(span, 2),
            frame.Payload.Length >= 8 ? MavLinkDecoderHelpers.ReadUInt32OrDefault(span, 4) : null,
            frame.ReceivedAt);

        return true;
    }
}
