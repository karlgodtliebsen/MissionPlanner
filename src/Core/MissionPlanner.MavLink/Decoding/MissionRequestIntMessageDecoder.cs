using MissionPlanner.MavLink.Decoding.Utils;
using MissionPlanner.MavLink.Messages;
using MissionPlanner.MavLink.Services.Abstractions;

namespace MissionPlanner.MavLink.Decoding;

public sealed class MissionRequestIntMessageDecoder : IMavLinkMessageDecoder
{
    public uint MessageId => MessageIds.MissionRequestInt;
    public byte CrcExtra => 196;

    public bool TryDecode(MavLinkFrame f, out MavLinkMessage? m)
    {
        m = null;
        if (f.MessageId != MessageId || f.Payload.Length < 4)
        {
            return false;
        }

        var s = f.Payload.Span;
        m = new MissionRequestIntMessage(f.SystemId, f.ComponentId, f.EndPoint, MavLinkDecoderHelpers.ReadUInt16OrDefault(s, 0), s[2], s[3], s.Length >= 5 ? s[4] : (byte)0, f.ReceivedAt);
        return true;
    }
}
