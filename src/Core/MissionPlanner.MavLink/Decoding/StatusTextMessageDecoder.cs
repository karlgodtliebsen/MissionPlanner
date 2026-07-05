using System.Buffers.Binary;
using MissionPlanner.MavLink.Messages;
using MissionPlanner.MavLink.Services;

namespace MissionPlanner.MavLink.Decoding;

/// <summary>
/// Decodes MAVLink statustext messages.
/// </summary>
public sealed class StatusTextMessageDecoder : IMavLinkMessageDecoder
{
    /// <inheritdoc/>
    public bool TryDecode(MavLinkFrame frame, out MavLinkMessage? message)
    {
        message = null;

        if (frame.MessageId != MessageIds.StatusText) return false;

        if (frame.Payload.Length < 2) return false;

        var payload = frame.Payload.Span;

        var severity = (MavSeverity)payload[0];

        var textLength = Math.Min(50, payload.Length - 1);
        var textBytes = payload.Slice(1, textLength);

        var nullIndex = textBytes.IndexOf((byte)0);

        if (nullIndex >= 0) textBytes = textBytes[..nullIndex];

        var text = System.Text.Encoding.ASCII.GetString(textBytes).TrimEnd();

        ushort? id = null;
        byte? chunkSequence = null;

        if (payload.Length >= 53)
            id = BinaryPrimitives.ReadUInt16LittleEndian(
                payload.Slice(51, 2));

        if (payload.Length >= 54) chunkSequence = payload[53];

        message = new StatusTextMessage(
            frame.SystemId,
            frame.ComponentId,
            frame.IPEndPoint,
            severity,
            text,
            id,
            chunkSequence,
            frame.ReceivedAt);

        return true;
    }
}