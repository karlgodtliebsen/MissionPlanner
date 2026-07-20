using MissionPlanner.MavLink.Decoding.Utils;
using MissionPlanner.MavLink.Messages;
using MissionPlanner.MavLink.Services.Abstractions;

namespace MissionPlanner.MavLink.Decoding;

/// <summary>
/// Provides the public API for MissionRequestIntMessageDecoder.
/// </summary>
public sealed class MissionRequestIntMessageDecoder : IMavLinkMessageDecoder
{
    /// <summary>
    /// Provides the public API for MessageId.
    /// </summary>
    public uint MessageId => MessageIds.MissionRequestInt;
    /// <summary>
    /// Provides the public API for CrcExtra.
    /// </summary>
    public byte CrcExtra => 196;

    /// <summary>
    /// Provides the public API for TryDecode.
    /// </summary>
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
