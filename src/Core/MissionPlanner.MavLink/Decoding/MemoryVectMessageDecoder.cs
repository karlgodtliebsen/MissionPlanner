using System.Buffers.Binary;
using MissionPlanner.MavLink.Messages;
using MissionPlanner.MavLink.Services.Abstractions;

namespace MissionPlanner.MavLink.Decoding;

/// <summary>
/// Decodes MAVLink MEMORY_VECT messages.
/// </summary>
public sealed class MemoryVectMessageDecoder : IMavLinkMessageDecoder
{
    /// <inheritdoc />
    public uint MessageId => MessageIds.MemoryVect;

    /// <inheritdoc />
    public byte CrcExtra => 204;

    /// <inheritdoc />
    public bool TryDecode(MavLinkFrame frame, out MavLinkMessage? message)
    {
        message = null;
        if (frame.MessageId != MessageId || frame.Payload.Length != 36)
        {
            return false;
        }

        var payload = frame.Payload.Span;
        var values = new sbyte[32];
        for (var index = 0; index < values.Length; index++)
        {
            values[index] = unchecked((sbyte)payload[index + 4]);
        }

        message = new MemoryVectMessage(
            frame.SystemId,
            frame.ComponentId,
            frame.EndPoint,
            BinaryPrimitives.ReadUInt16LittleEndian(payload),
            payload[2],
            payload[3],
            values,
            frame.ReceivedAt);
        return true;
    }
}
