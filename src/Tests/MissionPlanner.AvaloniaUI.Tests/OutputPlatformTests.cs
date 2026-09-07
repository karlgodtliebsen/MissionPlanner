using System.Net;
using System.Net.Sockets;
using MissionPlanner.Core.Setup.Advanced.Output;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Firmware.Devices;
using MissionPlanner.Library.Browser;
using MissionPlanner.Library.Windows;
using MissionPlanner.Transport.Abstractions;
using NSubstitute;

namespace MissionPlanner.AvaloniaUI.Tests;

public sealed class OutputPlatformTests
{
    [Fact]
    public async Task BrowserExplainsMissingAuditedBridgeWithoutOpeningNativeIo()
    {
        var factory = new BrowserOutputSinkFactory();
        var endpoint = new OutputEndpoint(OutputEndpointKind.Udp, "127.0.0.1");
        Assert.Contains("audited", factory.UnavailableReason(endpoint));
        await Assert.ThrowsAsync<NotSupportedException>(() => factory.OpenAsync(endpoint, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task WindowsUdpOutputDeliversExactDatagramAndDisposes()
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(5));
        using var listener = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var port = ((IPEndPoint)listener.Client.LocalEndPoint!).Port;
        var factory = new WindowsOutputSinkFactory(Substitute.For<IFirmwareSerialPortFactory>(),
            Substitute.For<IActiveVehicleContext>(), Substitute.For<IVehicleConnectionSession>(), Substitute.For<IVehicleRegistry>());
        await using var sink = await factory.OpenAsync(new(OutputEndpointKind.Udp, "127.0.0.1", port), deadline.Token);
        await sink.WriteAsync(new byte[] { 0xfe, 0, 1, 2, 3, 0xff }, deadline.Token);
        var result = await listener.ReceiveAsync(deadline.Token);
        Assert.Equal(new byte[] { 0xfe, 0, 1, 2, 3, 0xff }, result.Buffer);
        sink.Abort();
        sink.Abort();
    }

    [Fact]
    public async Task WindowsProtectsTheActiveVehicleSerialPortBeforeOpeningIt()
    {
        var ports = Substitute.For<IFirmwareSerialPortFactory>();
        var active = Substitute.For<IActiveVehicleContext>();
        active.IsOnline.Returns(true);
        var serial = Substitute.For<ISerialMavLinkTransport>();
        serial.PortName.Returns("COM11");
        var connection = Substitute.For<IVehicleConnectionSession>();
        connection.Transport.Returns(serial);
        var factory = new WindowsOutputSinkFactory(ports, active, connection, Substitute.For<IVehicleRegistry>());
        await Assert.ThrowsAsync<InvalidOperationException>(() => factory.OpenAsync(new(OutputEndpointKind.Serial, "com11"), TestContext.Current.CancellationToken));
        Assert.Empty(ports.ReceivedCalls());
    }
}
