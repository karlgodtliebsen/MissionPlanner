using System.Buffers.Binary;
using MissionPlanner.MavLink.Messages;
using MissionPlanner.MavLink.Services.Abstractions;

namespace MissionPlanner.MavLink.Decoding;

/// <summary>
/// Decodes MAVLink HOME_POSITION messages.
/// </summary>
public sealed class HomePositionMessageDecoder : IMavLinkMessageDecoder
{
    /// <inheritdoc/>
    public uint MessageId => MessageIds.HomePosition;

    /// <inheritdoc/>
    public byte CrcExtra => 104;

    /// <inheritdoc/>
    public bool TryDecode(MavLinkFrame frame, out MavLinkMessage? message)
    {
        message = null;

        if (frame.MessageId != MessageId)
        {
            return false;
        }

        // Required fields: latitude, longitude, altitude (3 x int32).
        if (frame.Payload.Length < 12)
        {
            return false;
        }

        var payload = frame.Payload.Span;

        var latitudeRaw = BinaryPrimitives.ReadInt32LittleEndian(payload.Slice(0, 4));
        var longitudeRaw = BinaryPrimitives.ReadInt32LittleEndian(payload.Slice(4, 4));
        var altitudeRaw = BinaryPrimitives.ReadInt32LittleEndian(payload.Slice(8, 4));

        message = new HomePositionMessage(
            frame.SystemId,
            frame.ComponentId,
            frame.EndPoint,
            latitudeRaw / 10_000_000.0,
            longitudeRaw / 10_000_000.0,
            altitudeRaw / 1000.0,
            frame.ReceivedAt);

        return true;
    }
}
