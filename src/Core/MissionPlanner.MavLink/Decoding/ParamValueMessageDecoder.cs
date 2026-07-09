using System.Buffers.Binary;
using MissionPlanner.MavLink.Messages;
using MissionPlanner.MavLink.Parameters;
using MissionPlanner.MavLink.Services;

namespace MissionPlanner.MavLink.Decoding;

/// <summary>
/// Decodes MAVLink PARAM_VALUE messages.
/// </summary>
public sealed class ParamValueMessageDecoder : IMavLinkMessageDecoder
{
    /// <inheritdoc />
    public uint MessageId { get; } = MessageIds.ParamValue;

    /// <inheritdoc />
    public byte CrcExtra { get; } = 22;

    /// <summary>
    /// Tries to decode a MAVLink PARAM_VALUE message from the given frame.
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

        // PARAM_VALUE payload layout (25 bytes):
        // float param_value     (4 bytes, offset 0)
        // uint16 param_count    (2 bytes, offset 4)
        // uint16 param_index    (2 bytes, offset 6)
        // char[16] param_id     (16 bytes, offset 8)
        // uint8 param_type      (1 byte, offset 24)

        if (frame.Payload.Length < 25)
        {
            return false;
        }

        var span = frame.Payload.Span;

        // Read float value
        var paramValue = BinaryPrimitives.ReadSingleLittleEndian(span[0..4]);

        // Read counts and index
        var paramCount = BinaryPrimitives.ReadUInt16LittleEndian(span[4..6]);
        var paramIndex = BinaryPrimitives.ReadUInt16LittleEndian(span[6..8]);

        // Read parameter ID (null-terminated string, max 16 chars)
        var paramIdBytes = span[8..24];
        var nullIndex = paramIdBytes.IndexOf((byte)0);
        var paramId = nullIndex >= 0
            ? System.Text.Encoding.ASCII.GetString(paramIdBytes[..nullIndex])
            : System.Text.Encoding.ASCII.GetString(paramIdBytes);

        // Read parameter type
        var paramType = (MavParamType)span[24];

        message = new ParamValueMessage(
            frame.SystemId,
            frame.ComponentId,
            frame.EndPoint,
            paramId,
            paramValue,
            paramType,
            paramCount,
            paramIndex,
            frame.ReceivedAt);

        return true;
    }
}
