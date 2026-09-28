using System.Buffers.Binary;
using MissionPlanner.MavLink;
using MissionPlanner.MavLink.Decoding;
using MissionPlanner.MavLink.Messages;
using MissionPlanner.Transport;

namespace MissionPlanner.Core.Tests;

/// <summary>Checks the BATTERY_STATUS wire offsets independently of application models.</summary>
public sealed class BatteryWireTests
{
    /// <summary>Instance, current and extensions use the MAVLink common wire layout, including zero truncation.</summary>
    [Theory]
    [InlineData(54)]
    [InlineData(51)]
    public void DecodesInstanceAndPartiallyTruncatedFault(int length)
    {
        var payload = new byte[54];
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(10), 20);
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(30), 1234);
        payload[32] = 2;
        payload[33] = 3;
        payload[34] = 4;
        payload[35] = 99;
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(41), 4200);
        payload[49] = 1;
        payload[50] = 8;
        var frame = new MavLinkFrame(16, 1, new TransportEndPoint("test"), 147, 0,
            payload.AsMemory(0, length), ReadOnlyMemory<byte>.Empty, DateTimeOffset.UtcNow);
        Assert.True(new BatteryStatusMessageDecoder().TryDecode(frame, out var decoded));
        var battery = Assert.IsType<BatteryStatusMessage>(decoded);
        Assert.Equal(2, battery.Id);
        Assert.Equal(1234, battery.CurrentBattery);
        Assert.Equal(99, battery.BatteryRemaining);
        Assert.Equal(4200, battery.VoltagesExt[0]);
        Assert.Equal(8u, battery.FaultBitmask);
    }
}
