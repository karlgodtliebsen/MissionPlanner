using MissionPlanner.MavLink.Decoding.Utils;
using MissionPlanner.MavLink.Messages;
using MissionPlanner.MavLink.Services.Abstractions;

namespace MissionPlanner.MavLink.Decoding;

/// <summary>
/// Provides the public API for MissionItemReachedMessageDecoder.
/// </summary>
public sealed class MissionItemReachedMessageDecoder : IMavLinkMessageDecoder
{
    /// <summary>
    /// Provides the public API for MessageId.
    /// </summary>
    public uint MessageId => MessageIds.MissionItemReached;
    /// <summary>
    /// Provides the public API for CrcExtra.
    /// </summary>
    public byte CrcExtra => 11;

    /// <summary>
    /// Provides the public API for TryDecode.
    /// </summary>
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
