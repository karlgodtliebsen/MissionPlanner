using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging.Abstractions;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.MavLink;
using MissionPlanner.MavLink.Decoding;
using MissionPlanner.MavLink.Decoding.Utils;
using MissionPlanner.MavLink.Services;

namespace MissionPlanner.Core.Tests;

public sealed class UdpVehicleDiscoveryTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ConnectCandidateRequiresValidArduPilotHeartbeat(bool sendArduPilot)
    {
        var definitions = new MavLinkMessageDefinitionRegistry();
        var decoder = new MavLinkMessageDecoderHandler(new MavLinkMessageDecoders(
            new MavLinkMessageDecoderCatalog(definitions).Decoders, definitions), NullLogger<MavLinkMessageDecoderHandler>.Instance);
        var discovery = new UdpVehicleDiscovery(new MavLinkV2FrameParser(definitions), decoder);
        int port;
        using (var reservation = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0)))
            port = ((IPEndPoint)reservation.Client.LocalEndPoint!).Port;
        using var sender = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var pending = discovery.FindAsync(port, TestContext.Current.CancellationToken);
        await sender.SendAsync(new byte[] { 1, 2, 3 }, new IPEndPoint(IPAddress.Loopback, port), TestContext.Current.CancellationToken);
        var packet = new byte[21];
        packet[0] = 0xfd;
        packet[1] = 9;
        packet[5] = 1;
        packet[6] = 1;
        packet[14] = 2; // quadrotor
        packet[15] = sendArduPilot ? (byte)3 : (byte)12;
        packet[17] = 4;
        packet[18] = 3;
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(19), MavLinkCrc.Calculate(packet.AsSpan(1, 18), 50));
        await sender.SendAsync(packet, new IPEndPoint(IPAddress.Loopback, port), TestContext.Current.CancellationToken);
        var result = await pending;
        if (sendArduPilot) Assert.Equal(sender.Client.LocalEndPoint, result);
        else Assert.Null(result);
        // The probe releases the listener so the normal connection can bind the same port.
        using var reopened = new UdpClient(new IPEndPoint(IPAddress.Any, port));
    }
}
