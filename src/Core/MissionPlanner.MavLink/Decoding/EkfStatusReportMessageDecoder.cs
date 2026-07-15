using MissionPlanner.MavLink.Decoding.Utils;
using MissionPlanner.MavLink.Messages;
using MissionPlanner.MavLink.Services.Abstractions;

namespace MissionPlanner.MavLink.Decoding;

/// <summary>
/// Decodes ArduPilot MAVLink EKF_STATUS_REPORT messages.
/// </summary>
public sealed class EkfStatusReportMessageDecoder : IMavLinkMessageDecoder
{
    /// <inheritdoc />
    public uint MessageId { get; } = MessageIds.EkfStatusReport;

    /// <inheritdoc />
    public byte CrcExtra { get; } = 71;

    /// <inheritdoc />
    public bool TryDecode(MavLinkFrame frame, out MavLinkMessage? message)
    {
        message = null;

        if (frame.MessageId != MessageId)
        {
            return false;
        }

        if (frame.Payload.Length < 22)
        {
            return false;
        }

        var span = frame.Payload.Span;

        message = new EkfStatusReportMessage(
            frame.SystemId,
            frame.ComponentId,
            frame.EndPoint,
            MavLinkDecoderHelpers.ReadUInt16OrDefault(span, 20),
            MavLinkDecoderHelpers.ReadSingleOrDefault(span, 0),
            MavLinkDecoderHelpers.ReadSingleOrDefault(span, 4),
            MavLinkDecoderHelpers.ReadSingleOrDefault(span, 8),
            MavLinkDecoderHelpers.ReadSingleOrDefault(span, 12),
            MavLinkDecoderHelpers.ReadSingleOrDefault(span, 16),
            frame.Payload.Length >= 26 ? MavLinkDecoderHelpers.ReadSingleOrDefault(span, 22) : null,
            frame.ReceivedAt);

        return true;
    }
}
