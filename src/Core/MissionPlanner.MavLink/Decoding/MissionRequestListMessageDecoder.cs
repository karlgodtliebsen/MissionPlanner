using MissionPlanner.MavLink.Messages;
using MissionPlanner.MavLink.Services.Abstractions;

namespace MissionPlanner.MavLink.Decoding;

/// <summary>
/// Decodes MAVLink MISSION_REQUEST_LIST messages, including the optional mission type.
/// </summary>
public sealed class MissionRequestListMessageDecoder : IMavLinkMessageDecoder
{
    /// <inheritdoc />
    public uint MessageId => MessageIds.MissionRequestList;

    /// <inheritdoc />
    public byte CrcExtra => 132;

    /// <inheritdoc />
    public bool TryDecode(MavLinkFrame frame, out MavLinkMessage? message)
    {
        message = null;
        if (frame.MessageId != MessageId || frame.Payload.Length is < 2 or > 3)
        {
            return false;
        }

        var payload = frame.Payload.Span;
        message = new MissionRequestListMessage(
            frame.SystemId,
            frame.ComponentId,
            frame.EndPoint,
            payload[0],
            payload[1],
            payload.Length == 3 ? payload[2] : (byte)0,
            frame.ReceivedAt);
        return true;
    }
}
