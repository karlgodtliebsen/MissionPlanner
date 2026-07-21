using System.Buffers.Binary;
using MissionPlanner.MavLink.Messages;
using MissionPlanner.MavLink.Services.Abstractions;

namespace MissionPlanner.MavLink.Decoding;

/// <summary>
/// Decodes MAVLink AUTOPILOT_VERSION messages, including truncated MAVLink 2 extensions.
/// </summary>
public sealed class AutopilotVersionMessageDecoder : IMavLinkMessageDecoder
{
    /// <inheritdoc />
    public uint MessageId => MessageIds.AutopilotVersion;

    /// <inheritdoc />
    public byte CrcExtra => 178;

    /// <inheritdoc />
    public bool TryDecode(MavLinkFrame frame, out MavLinkMessage? message)
    {
        message = null;
        if (frame.MessageId != MessageId || frame.Payload.Length < 60)
        {
            return false;
        }

        Span<byte> payload = stackalloc byte[78];
        payload.Clear();
        frame.Payload.Span[..Math.Min(frame.Payload.Length, payload.Length)].CopyTo(payload);
        message = new AutopilotVersionMessage(
            frame.SystemId,
            frame.ComponentId,
            frame.EndPoint,
            BinaryPrimitives.ReadUInt64LittleEndian(payload[0..8]),
            BinaryPrimitives.ReadUInt32LittleEndian(payload[16..20]),
            BinaryPrimitives.ReadUInt32LittleEndian(payload[20..24]),
            BinaryPrimitives.ReadUInt32LittleEndian(payload[24..28]),
            BinaryPrimitives.ReadUInt32LittleEndian(payload[28..32]),
            payload[36..44].ToArray(),
            payload[44..52].ToArray(),
            payload[52..60].ToArray(),
            BinaryPrimitives.ReadUInt16LittleEndian(payload[32..34]),
            BinaryPrimitives.ReadUInt16LittleEndian(payload[34..36]),
            BinaryPrimitives.ReadUInt64LittleEndian(payload[8..16]),
            payload[60..78].ToArray(),
            frame.ReceivedAt);
        return true;
    }
}
