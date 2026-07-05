using System.Buffers.Binary;
using MissionPlanner.MavLink.Messages;
using MissionPlanner.MavLink.Services;

namespace MissionPlanner.MavLink.Decoding;

/// <inheritdoc />
public sealed class SysStatusMessageDecoder : IMavLinkMessageDecoder
{
    /// <inheritdoc />  
    public bool TryDecode(MavLinkFrame frame, out MavLinkMessage? message)
    {
        message = null;

        if (frame.MessageId != MessageIds.SysStatus) return false;

        if (frame.Payload.Length < 31) return false;

        var span = frame.Payload.Span;

        var voltageBatteryMv =
            BinaryPrimitives.ReadUInt16LittleEndian(span[14..16]);

        var batteryRemainingRaw = unchecked((sbyte)span[30]);

        float? voltage =
            voltageBatteryMv == ushort.MaxValue
                ? null
                : voltageBatteryMv / 1000.0f;

        int? batteryRemaining =
            batteryRemainingRaw < 0
                ? null
                : batteryRemainingRaw;

        message = new SysStatusMessage(
            frame.SystemId,
            frame.ComponentId,
            frame.IPEndPoint,
            batteryRemaining,
            voltage,
            frame.ReceivedAt);

        return true;
    }
}