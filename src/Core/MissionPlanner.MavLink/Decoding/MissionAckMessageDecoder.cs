using MissionPlanner.MavLink.Messages;
using MissionPlanner.MavLink.Services.Abstractions;

namespace MissionPlanner.MavLink.Decoding;

/// <summary>
/// Provides the public API for MissionAckMessageDecoder.
/// </summary>
public sealed class MissionAckMessageDecoder : IMavLinkMessageDecoder
{
    /// <summary>
    /// Provides the public API for MessageId.
    /// </summary>
    public uint MessageId => MessageIds.MissionAck;
    /// <summary>
    /// Provides the public API for CrcExtra.
    /// </summary>
    public byte CrcExtra => 153;

    /// <summary>
    /// Provides the public API for TryDecode.
    /// </summary>
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
