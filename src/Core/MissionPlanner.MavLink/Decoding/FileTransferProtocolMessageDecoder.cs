using MissionPlanner.MavLink.Messages;
using MissionPlanner.MavLink.Services.Abstractions;

namespace MissionPlanner.MavLink.Decoding;

public sealed class FileTransferProtocolMessageDecoder : IMavLinkMessageDecoder
{
    public uint MessageId => MessageIds.FileTransferProtocol;
    public byte CrcExtra => 84;

    public bool TryDecode(MavLinkFrame frame, out MavLinkMessage? message)
    {
        if (frame.Payload.Length < 3)
        {
            message = null;
            return false;
        }
        var payload = frame.Payload.Span;
        message = new FileTransferProtocolMessage(frame.SystemId, frame.ComponentId, frame.EndPoint,
            payload[0], payload[1], payload[2], payload[3..].ToArray(), frame.ReceivedAt);
        return true;
    }
}
