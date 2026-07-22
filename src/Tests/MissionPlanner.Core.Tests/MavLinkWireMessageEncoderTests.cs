using FluentAssertions;
using MissionPlanner.MavLink;
using MissionPlanner.MavLink.Encoding;
using MissionPlanner.MavLink.Generated;
using MissionPlanner.MavLink.Messages;
using MissionPlanner.MavLink.Services;
using MissionPlanner.Transport;

namespace MissionPlanner.Core.Tests;

/// <summary>Verifies general generated-message MAVLink 2 encoding.</summary>
public sealed class MavLinkWireMessageEncoderTests
{
    /// <summary>Verifies a DEVICE_OP_READ message encodes with its 24-bit ID and registered CRC extra.</summary>
    [Fact]
    public void EncodesDeviceOperationReadPacket()
    {
        var crcProvider = new CommonMavLinkCrcExtraProvider();
        var encoder = new MavLinkWireMessageEncoder(crcProvider);
        var message = new DeviceOpReadMessage(
            255,
            190,
            new TransportEndPoint("test"),
            1,
            1,
            42,
            (byte)DeviceOpBustype.I2c,
            0,
            0x50,
            string.Empty,
            0,
            100,
            0,
            DateTimeOffset.UtcNow);

        var packet = encoder.Encode(message);

        packet[0].Should().Be(0xfd);
        packet[5].Should().Be(255);
        packet[6].Should().Be(190);
        ((uint)packet[7] | ((uint)packet[8] << 8) | ((uint)packet[9] << 16)).Should().Be(11000);
        packet[14].Should().Be(1);
        packet[15].Should().Be(1);
        packet[18].Should().Be(0x50);
        crcProvider.TryGetCrcExtra(11000, out var crcExtra).Should().BeTrue();
        var crc = MavLinkCrc.Calculate(packet.AsSpan(1, packet.Length - 3), crcExtra);
        packet[^2].Should().Be((byte)crc);
        packet[^1].Should().Be((byte)(crc >> 8));
    }
}
