using MissionPlanner.MavLink.Decoding.Utils;
using MissionPlanner.MavLink.Messages;
using MissionPlanner.MavLink.Services;

namespace MissionPlanner.MavLink.Decoding;

public sealed class MissionItemReachedMessageDecoder : IMavLinkMessageDecoder
{
    public uint MessageId => MessageIds.MissionItemReached;
    public byte CrcExtra => 11;

    public bool TryDecode(MavLinkFrame f, out MavLinkMessage? m)
    {
        m = null;
        if (f.MessageId != MessageId || f.Payload.Length < 2)
        {
            return false;
        }

        m = new MissionItemReachedMessage(f.SystemId, f.ComponentId, f.EndPoint, MavLinkDecoderHelpers.ReadUInt16OrDefault(f.Payload.Span, 0), f.ReceivedAt);
        return true;
    }
}
