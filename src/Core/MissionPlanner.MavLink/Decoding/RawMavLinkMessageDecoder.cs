using MissionPlanner.MavLink.Messages;
using MissionPlanner.MavLink.Services;

namespace MissionPlanner.MavLink.Decoding;

/// <summary>
/// Fallback decoder for CRC-valid MAVLink frames without a specific decoder yet.
/// Keep this decoder last in the decoder list.
/// </summary>
public sealed class RawMavLinkMessageDecoder : IMavLinkMessageDecoder
{
    /// <inheritdoc />
    public uint MessageId { get; } = MessageIds.DefaultFallback;

    /// <inheritdoc />
    public byte CrcExtra { get; } = 0;

    /// <inheritdoc/>
    public bool TryDecode(MavLinkFrame frame, out MavLinkMessage? message)
    {
        message = new RawMavLinkMessage(
            frame.SystemId,
            frame.ComponentId,
            frame.EndPoint,
            frame.MessageId,
            frame.Sequence,
            frame.Payload.ToArray(),
            frame.RawBytes.ToArray(),
            frame.ReceivedAt);

        return true;
    }
}
