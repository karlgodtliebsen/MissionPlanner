using System.Buffers.Binary;
using MissionPlanner.MavLink.Messages;
using MissionPlanner.MavLink.Services.Abstractions;

namespace MissionPlanner.MavLink.Decoding;

/// <summary>
/// Decodes MAVLink Heartbeat messages.
/// </summary>
public sealed class HeartbeatMessageDecoder : IMavLinkMessageDecoder
{
    /// <inheritdoc />
    public uint MessageId { get; } = MessageIds.Heartbeat;

    /// <inheritdoc />
    public byte CrcExtra { get; } = 50;

    /// <summary>
    /// Tries to decode a MAVLink Heartbeat message from the given frame.
    /// </summary>
    /// <param name="frame">The MAVLink frame containing the message.</param>
    /// <param name="message">The decoded MAVLink message, if successful.</param>
    /// <returns>True if the message was successfully decoded; otherwise, false.</returns>
    public bool TryDecode(MavLinkFrame frame, out MavLinkMessage? message)
    {
        message = null;

        if (frame.MessageId != MessageId)
        {
            return false;
        }

        //        if (frame.Payload.Length != 9) return false;
        if (frame.Payload.Length < 9)
        {
            return false;
        }

        var span = frame.Payload.Span;

        var customMode = BinaryPrimitives.ReadUInt32LittleEndian(span[0..4]);

        message = new HeartbeatMessage(
            frame.SystemId,
            frame.ComponentId,
            frame.EndPoint,
            customMode,
            span[4],
            span[5],
            span[6],
            span[7],
            span[8],
            frame.ReceivedAt);

        return true;
    }
}
