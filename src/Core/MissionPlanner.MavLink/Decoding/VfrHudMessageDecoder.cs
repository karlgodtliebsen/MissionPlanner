using MissionPlanner.MavLink.Decoding.Utils;
using MissionPlanner.MavLink.Messages;
using MissionPlanner.MavLink.Services.Abstractions;

namespace MissionPlanner.MavLink.Decoding;

/// <summary>
/// Decodes MAVLink VFR_HUD messages.
/// </summary>
public sealed class VfrHudMessageDecoder : IMavLinkMessageDecoder
{
    /// <inheritdoc />
    public uint MessageId { get; } = MessageIds.VfrHud;

    /// <inheritdoc />
    public byte CrcExtra { get; } = 20;

    /// <inheritdoc />
    public bool TryDecode(MavLinkFrame frame, out MavLinkMessage? message)
    {
        message = null;

        if (frame.MessageId != MessageId)
        {
            return false;
        }

        if (frame.Payload.Length is < 1 or > 20)
        {
            return false;
        }

        Span<byte> span = stackalloc byte[20];
        span.Clear();
        frame.Payload.Span.CopyTo(span);

        message = new VfrHudMessage(
            frame.SystemId,
            frame.ComponentId,
            frame.EndPoint,
            MavLinkDecoderHelpers.ReadSingleOrDefault(span, 0),
            MavLinkDecoderHelpers.ReadSingleOrDefault(span, 4),
            MavLinkDecoderHelpers.ReadInt16OrDefault(span, 16),
            MavLinkDecoderHelpers.ReadUInt16OrDefault(span, 18),
            MavLinkDecoderHelpers.ReadSingleOrDefault(span, 8),
            MavLinkDecoderHelpers.ReadSingleOrDefault(span, 12),
            frame.ReceivedAt);

        return true;
    }
}
