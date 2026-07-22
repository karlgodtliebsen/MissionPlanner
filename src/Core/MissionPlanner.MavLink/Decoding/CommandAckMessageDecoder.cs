using System.Buffers.Binary;
using MissionPlanner.MavLink.Messages;
using MissionPlanner.MavLink.Services.Abstractions;

namespace MissionPlanner.MavLink.Decoding;

/// <summary>
/// 
/// </summary>
public sealed class CommandAckMessageDecoder : IMavLinkMessageDecoder
{
    /// <inheritdoc />
    public uint MessageId { get; } = MessageIds.CommandAck;

    /// <inheritdoc />
    public byte CrcExtra { get; } = 143;

    /// <inheritdoc/>
    public bool TryDecode(MavLinkFrame frame, out MavLinkMessage? message)
    {
        message = null;

        if (frame.MessageId != MessageId)
        {
            return false;
        }

        if (frame.Payload.Length < 3)
        {
            return false;
        }

        var span = frame.Payload.Span;

        var command = BinaryPrimitives.ReadUInt16LittleEndian(span[0..2]);

        var result = span[2];
        var progress = span.Length > 3 ? span[3] : byte.MaxValue;
        var resultParameter2 = span.Length >= 8 ? BinaryPrimitives.ReadInt32LittleEndian(span[4..8]) : 0;
        var targetSystemId = span.Length > 8 ? span[8] : (byte?)null;
        var targetComponentId = span.Length > 9 ? span[9] : (byte?)null;

        message = new CommandAckMessage(
            frame.SystemId,
            frame.ComponentId,
            frame.EndPoint,
            command,
            result,
            frame.ReceivedAt,
            progress,
            resultParameter2,
            targetSystemId,
            targetComponentId);

        return true;
    }
}
