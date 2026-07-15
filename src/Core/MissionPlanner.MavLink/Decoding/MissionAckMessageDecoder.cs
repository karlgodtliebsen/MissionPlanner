using MissionPlanner.MavLink.Messages;
using MissionPlanner.MavLink.Services;

namespace MissionPlanner.MavLink.Decoding;

public sealed class MissionAckMessageDecoder : IMavLinkMessageDecoder
{
    public uint MessageId => MessageIds.MissionAck;
    public byte CrcExtra => 153;

    public bool TryDecode(MavLinkFrame f, out MavLinkMessage? m)
    {
        m = null;
        if (f.MessageId != MessageId || f.Payload.Length < 3)
        {
            return false;
        }

        var s = f.Payload.Span;
        m = new MissionAckMessage(f.SystemId, f.ComponentId, f.EndPoint, s[0], s[1], s[2], s.Length >= 4 ? s[3] : (byte)0, f.ReceivedAt);
        return true;
    }
}
