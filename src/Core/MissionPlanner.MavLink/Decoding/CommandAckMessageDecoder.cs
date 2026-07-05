using System.Buffers.Binary;
using MissionPlanner.MavLink.Messages;
using MissionPlanner.MavLink.Services;

namespace MissionPlanner.MavLink.Decoding;

/// <summary>
/// 
/// </summary>
public sealed class CommandAckMessageDecoder : IMavLinkMessageDecoder
{
    /// <inheritdoc/>
    public bool TryDecode(MavLinkFrame frame, out MavLinkMessage? message)
    {
        message = null;

        if (frame.MessageId != MessageIds.CommandAck) return false;

        if (frame.Payload.Length < 3) return false;

        var span = frame.Payload.Span;

        var command = BinaryPrimitives.ReadUInt16LittleEndian(span[0..2]);

        var result = span[2];

        message = new CommandAckMessage(
            frame.SystemId,
            frame.ComponentId,
            frame.IPEndPoint,
            command,
            result,
            frame.ReceivedAt);

        return true;
    }
}