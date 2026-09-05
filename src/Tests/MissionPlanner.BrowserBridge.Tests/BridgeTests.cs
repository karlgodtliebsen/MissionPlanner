using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using MissionPlanner.BrowserBridge;
using MissionPlanner.Library.Browser.Transport;
using MissionPlanner.Transport;

namespace MissionPlanner.BrowserBridge.Tests;

public sealed class BridgeTests
{
    [Fact]
    public async Task RelayPreservesPacketsPinsPeerRejectsOtherClientsAndReleasesPort()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var ct = deadline.Token;
        int port;
        using (var reserve = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0)))
            port = ((IPEndPoint)reserve.Client.LocalEndPoint!).Port;
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        await using var app = builder.Build();
        var bridge = new UdpBridge(port);
        app.UseWebSockets();
        app.Map("/bridge/udp", bridge.HandleAsync);
        await app.StartAsync(ct);
        var origin = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        var uri = new Uri(origin.Replace("http:", "ws:") + $"/bridge/udp?port={port}");
        using (var foreign = new ClientWebSocket())
        {
            foreign.Options.SetRequestHeader("Origin", "https://unrelated.example");
            await Assert.ThrowsAsync<WebSocketException>(() => foreign.ConnectAsync(uri, ct));
        }
        using (var missingOrigin = new ClientWebSocket())
            await Assert.ThrowsAsync<WebSocketException>(() => missingOrigin.ConnectAsync(uri, ct));
        using (var wrongPort = new ClientWebSocket())
        {
            wrongPort.Options.SetRequestHeader("Origin", origin);
            await Assert.ThrowsAsync<WebSocketException>(() => wrongPort.ConnectAsync(new Uri(uri.GetLeftPart(UriPartial.Path) + "?port=1"), ct));
        }
        using var ws = new ClientWebSocket();
        ws.Options.SetRequestHeader("Origin", origin);
        await using var transport = new BrowserUdpTransport(uri, ws);
        await transport.ConnectAsync(ct);
        using (var competing = new ClientWebSocket())
        {
            competing.Options.SetRequestHeader("Origin", origin);
            await Assert.ThrowsAsync<WebSocketException>(() => competing.ConnectAsync(uri, ct));
        }
        using var vehicle = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var destination = new IPEndPoint(IPAddress.Loopback, port);
        byte[] packet = [0xfe, 9, 0, 1, 1, 0, 0, 0, 0, 0, 2, 3, 81, 4, 3, 0, 0];
        await vehicle.SendAsync(packet, destination, ct);
        var received = new List<byte>();
        var smallBuffer = new byte[5];
        TransportEndPoint? peer = null;
        while (received.Count < packet.Length)
        {
            var result = await transport.ReadAsync(smallBuffer, ct);
            received.AddRange(smallBuffer.Take(result.BytesRead));
            peer = result.RemoteEndpoint;
        }
        Assert.Equal(packet, received);
        Assert.Equal(((IPEndPoint)vehicle.Client.LocalEndPoint!).Port, peer!.ToIPEndPoint().Port);
        await transport.WriteAsync(packet, peer, ct);
        Assert.Equal(packet, (await vehicle.ReceiveAsync(ct)).Buffer);

        // A fragmented WebSocket message must remain one UDP datagram.
        await ws.SendAsync(packet.AsMemory(0, 4), WebSocketMessageType.Binary, false, ct);
        await ws.SendAsync(packet.AsMemory(4), WebSocketMessageType.Binary, true, ct);
        Assert.Equal(packet, (await vehicle.ReceiveAsync(ct)).Buffer);
        using var intruder = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        await intruder.SendAsync(new byte[] { 99 }, destination, ct);
        await vehicle.SendAsync(new byte[] { 42 }, destination, ct);
        var next = await transport.ReadAsync(smallBuffer, ct);
        Assert.Equal(1, next.BytesRead);
        Assert.Equal(42, smallBuffer[0]);
        await transport.DisconnectAsync(ct);
        await app.StopAsync(ct);
        using var reuse = new UdpClient(destination); // No leaked port after disconnect.
    }
}
