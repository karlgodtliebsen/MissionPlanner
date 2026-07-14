using System.Buffers.Binary;
using MissionPlanner.MavLink.Messages;
using MissionPlanner.MavLink.Services;

namespace MissionPlanner.MavLink.Decoding;

/// <inheritdoc />
public sealed class GlobalPositionIntMessageDecoder : IMavLinkMessageDecoder
{
    /// <inheritdoc/>
    public uint MessageId => MessageIds.GlobalPositionInt;

    /// <inheritdoc/>
    public byte CrcExtra => 104;

    /// <inheritdoc/>
    public bool TryDecode(
        MavLinkFrame frame,
        out MavLinkMessage? message)
    {
        message = null;

        if (frame.MessageId != MessageId)
        {
            return false;
        }

        // Required fields through alt.
        if (frame.Payload.Length < 16)
        {
            return false;
        }

        var payload = frame.Payload.Span;

        var latitudeRaw =
            BinaryPrimitives.ReadInt32LittleEndian(payload.Slice(4, 4));

        var longitudeRaw =
            BinaryPrimitives.ReadInt32LittleEndian(payload.Slice(8, 4));

        var altitudeRaw =
            BinaryPrimitives.ReadInt32LittleEndian(payload.Slice(12, 4));

        int? relativeAltitudeRaw = payload.Length >= 20
            ? BinaryPrimitives.ReadInt32LittleEndian(payload.Slice(16, 4))
            : null;

        short? velocityNorthRaw = payload.Length >= 22
            ? BinaryPrimitives.ReadInt16LittleEndian(payload.Slice(20, 2))
            : null;

        short? velocityEastRaw = payload.Length >= 24
            ? BinaryPrimitives.ReadInt16LittleEndian(payload.Slice(22, 2))
            : null;

        short? velocityDownRaw = payload.Length >= 26
            ? BinaryPrimitives.ReadInt16LittleEndian(payload.Slice(24, 2))
            : null;

        ushort? headingRaw = payload.Length >= 28
            ? BinaryPrimitives.ReadUInt16LittleEndian(payload.Slice(26, 2))
            : null;

        var headingDegrees = headingRaw is null or ushort.MaxValue ? 0.0 : headingRaw.Value / 100.0;

        message = new GlobalPositionIntMessage(
            frame.SystemId,
            frame.ComponentId,
            frame.EndPoint,
            latitudeRaw / 10_000_000.0,
            longitudeRaw / 10_000_000.0,
            altitudeRaw / 1000.0,
            relativeAltitudeRaw / 1000.0,
            velocityNorthRaw / 100.0,
            velocityEastRaw / 100.0,
            velocityDownRaw / 100.0,
            headingDegrees,
            frame.ReceivedAt);

        return true;
    }
}
