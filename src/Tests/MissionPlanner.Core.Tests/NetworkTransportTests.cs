using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MissionPlanner.Core.Commands;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.MavLink;
using MissionPlanner.MavLink.Decoding.Utils;
using MissionPlanner.MavLink.Services;
using MissionPlanner.Shared.Models.Vehicles.Models;
using MissionPlanner.Test.Support.Configuration;
using MissionPlanner.Transport;

namespace MissionPlanner.Core.Tests;

public sealed class NetworkTransportTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PipelineDiscoversVehiclesDownloadsParametersAcknowledgesAndReconnects(bool websocket)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(35));
        var token = timeout.Token;
        await using var provider = TestConfigurator.AddTestConfiguration(null).BuildServiceProvider();
        provider.UseTestConfiguration();
        await using var fixture = new BinaryVehicleFixture(websocket);
        var connection = provider.GetRequiredService<IVehicleConnectionService>();
        var registry = provider.GetRequiredService<IVehicleRegistry>();
        var session = provider.GetRequiredService<IVehicleConnectionSession>();
        var vehicles = provider.GetRequiredService<IVehicleService>();
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var result = await connection.ConnectNetworkAsync(fixture.Settings, cancellationToken: token);
            Assert.True(result.Success, result.ErrorMessage);
            await Eventually(() => registry.Vehicles.Count == 2, token);
            var id = new VehicleId(42, 1);
            await Eventually(() => vehicles.GetVehicleState(id)?.Roll is > 0.1, token);
            Assert.True(await session.ParameterService.RequestParameterListAsync(id, token));
            await Eventually(() => session.ParameterRegistry.GetAllParameters(id).ContainsKey("TEST_VALUE"), token);
            Assert.Equal(42, session.ParameterRegistry.GetAllParameters(id)["TEST_VALUE"].Value);
            var response = await provider.GetRequiredService<IVehicleCommandService>().DisarmAsync(id, true, token);
            Assert.True(response.Result == VehicleCommandResult.Accepted, response.Message);
            Assert.Contains((42, 400), fixture.Commands);
            Assert.DoesNotContain((41, 400), fixture.Commands);
            await connection.DisconnectAsync(token);
            Assert.False(connection.IsConnected);
        }
        Assert.True(fixture.GcsHeartbeats >= 2);
    }

    [Fact]
    public async Task UdpFiltersOtherPeersRetainsLargeDatagramsAndHonorsCancellation()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var token = timeout.Token;
        using var server = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        using var stranger = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        await using var transport = new UdpMavLinkTransport(Options.Create(new TransportEndpoint
        {
            IsUdpClient = true, LocalHost = "127.0.0.1", LocalPort = 0, RemoteHost = "localhost",
            RemotePort = ((IPEndPoint)server.Client.LocalEndPoint!).Port
        }), NullLogger<UdpMavLinkTransport>.Instance);
        await transport.ConnectAsync(token);
        await transport.WriteAsync(new byte[] { 1 }, transport.RemotePeer!, token);
        var hello = await server.ReceiveAsync(token);
        await stranger.SendAsync(new byte[] { 99 }, hello.RemoteEndPoint, token);
        var bytes = Enumerable.Range(0, 2048).Select(i => (byte)i).ToArray();
        await server.SendAsync(bytes, hello.RemoteEndPoint, token);
        var received = new List<byte>();
        var buffer = new byte[73];
        while (received.Count < bytes.Length)
        {
            var result = await transport.ReadAsync(buffer, token);
            received.AddRange(buffer.Take(result.BytesRead));
        }
        Assert.Equal(bytes, received);
        for (var i = 0; i < 2; i++)
        {
            await server.SendAsync(new byte[] { 7 }, hello.RemoteEndPoint, token);
        }
        for (var i = 0; i < 2; i++)
        {
            Assert.Equal(1, (await transport.ReadAsync(buffer, token)).BytesRead);
            Assert.Equal(7, buffer[0]);
        }
        using var cancelled = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await transport.ReadAsync(buffer, cancelled.Token));
    }

    [Fact]
    public async Task WebSocketRejectsTextAndRedactsUrl()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await using var fixture = new BinaryVehicleFixture(true, textOnly: true);
        await using var transport = new WebSocketMavLinkTransport(Options.Create(new TransportEndpoint
        {
            WebSocketUrl = fixture.Settings.WebSocketUrl + "?token=secret-value"
        }));
        await transport.ConnectAsync(timeout.Token);
        var error = await Assert.ThrowsAsync<IOException>(async () => await transport.ReadAsync(new byte[1024], timeout.Token));
        Assert.Contains("raw binary", error.Message);
        Assert.DoesNotContain("secret-value", error.ToString());
        Assert.DoesNotContain("secret-value", transport.RemotePeer.ToString());
    }

    [Fact]
    public async Task WssRejectsUntrustedCertificatesWithoutLeakingQuery()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        var token = timeout.Token;
        using var key = System.Security.Cryptography.RSA.Create(2048);
        var request = new System.Security.Cryptography.X509Certificates.CertificateRequest("CN=localhost", key,
            System.Security.Cryptography.HashAlgorithmName.SHA256, System.Security.Cryptography.RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddDays(1));
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var server = Task.Run(async () =>
        {
            using var client = await listener.AcceptTcpClientAsync(token);
            using var tls = new System.Net.Security.SslStream(client.GetStream());
            try
            {
                await tls.AuthenticateAsServerAsync(new System.Net.Security.SslServerAuthenticationOptions
                {
                    ServerCertificate = certificate
                }, token);
                await tls.ReadExactlyAsync(new byte[1], token);
            }
            catch (Exception exception) when (exception is IOException or System.Security.Authentication.AuthenticationException) { }
        }, token);
        try
        {
            await using var transport = new WebSocketMavLinkTransport(Options.Create(new TransportEndpoint
            {
                WebSocketUrl = $"wss://127.0.0.1:{port}/private?token=hidden"
            }));
            var error = await Assert.ThrowsAsync<IOException>(() => transport.ConnectAsync(token));
            Assert.Contains("certificate", error.Message);
            Assert.DoesNotContain("hidden", error.ToString());
            await server;
        }
        finally
        {
            listener.Stop();
        }
    }

    [Fact]
    public async Task SilentUdpEndpointProducesHeartbeatTimeout()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        using var server = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        await using var provider = TestConfigurator.AddTestConfiguration(null).BuildServiceProvider();
        provider.UseTestConfiguration();
        var connection = provider.GetRequiredService<IVehicleConnectionService>();
        var result = await connection.ConnectNetworkAsync(new()
        {
            Channel = "UDPCl", LocalPort = 0, LocalBindAddress = "127.0.0.1", RemoteHost = "127.0.0.1",
            RemotePort = ((IPEndPoint)server.Client.LocalEndPoint!).Port
        }, cancellationToken: timeout.Token);
        Assert.False(result.Success);
        Assert.Contains("heartbeat", result.ErrorMessage);
        Assert.False(connection.IsConnected);
    }
    [Fact]
    public async Task UdpListenRecoversAfterMalformedAndMissingDatagrams()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var token = timeout.Token;
        int port;
        using (var reservation = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0)))
        {
            port = ((IPEndPoint)reservation.Client.LocalEndPoint!).Port;
        }
        await using var transport = new UdpMavLinkTransport(Options.Create(new TransportEndpoint
        {
            LocalHost = "127.0.0.1", LocalPort = port
        }), NullLogger<UdpMavLinkTransport>.Instance);
        await transport.ConnectAsync(token);
        using var sender = new UdpClient();
        var target = new IPEndPoint(IPAddress.Loopback, port);
        await sender.SendAsync(new byte[] { 1, 2, 3 }, target, token);
        foreach (byte sequence in new byte[] { 0, 2, 2 })
        {
            await sender.SendAsync(MavLinkKnownFrames.CreateHeartbeatV2(new CommonMavLinkCrcExtraProvider(), sequence), target, token);
        }
        var parser = new MavLinkV2FrameParser(new MavLinkMessageDefinitionRegistry());
        var frames = new List<MavLinkFrame>();
        var buffer = new byte[512];
        for (var i = 0; i < 4; i++)
        {
            var result = await transport.ReadAsync(buffer, token);
            Assert.NotNull(result.RemoteEndpoint);
            frames.AddRange(parser.Parse(buffer.AsSpan(0, result.BytesRead), result.RemoteEndpoint, DateTimeOffset.UtcNow));
        }
        Assert.Equal(new byte[] { 0, 2, 2 }, frames.Select(frame => frame.Sequence));
    }

    private static async Task Eventually(Func<bool> check, CancellationToken token)
    {
        while (!check())
        {
            await Task.Delay(20, token);
        }
    }

    private sealed class BinaryVehicleFixture : IAsyncDisposable
    {
        private readonly CancellationTokenSource lifetime = new();
        private readonly UdpClient? udp;
        private readonly HttpListener? listener;
        private readonly Task worker;
        private readonly bool textOnly;
        private readonly MavLinkMessageDefinitionRegistry definitions = new();
        private byte sequence;
        public int GcsHeartbeats;
        public ConcurrentBag<(int System, int Command)> Commands { get; } = [];
        public NetworkConnectionSettings Settings { get; }

        public BinaryVehicleFixture(bool websocket, bool textOnly = false)
        {
            this.textOnly = textOnly;
            if (websocket)
            {
                var reservation = new TcpListener(IPAddress.Loopback, 0);
                reservation.Start();
                var port = ((IPEndPoint)reservation.LocalEndpoint).Port;
                reservation.Stop();
                listener = new HttpListener();
                listener.Prefixes.Add($"http://127.0.0.1:{port}/");
                listener.Start();
                Settings = new() { Channel = "WS", WebSocketUrl = $"ws://127.0.0.1:{port}/mavlink" };
                worker = RunWebSocket();
            }
            else
            {
                udp = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
                Settings = new() { Channel = "UDPCl", RemoteHost = "localhost", RemotePort = ((IPEndPoint)udp.Client.LocalEndPoint!).Port,
                    LocalBindAddress = "127.0.0.1", LocalPort = 0 };
                worker = RunUdp();
            }
        }

        private async Task RunUdp()
        {
            var parser = new MavLinkV2FrameParser(definitions);
            try
            {
                while (true)
                {
                    var packet = await udp!.ReceiveAsync(lifetime.Token);
                    foreach (var reply in Reply(parser.Parse(packet.Buffer, new("udp", packet.RemoteEndPoint), DateTimeOffset.UtcNow)))
                    {
                        await udp.SendAsync(reply, packet.RemoteEndPoint, lifetime.Token);
                    }
                }
            }
            catch (Exception) when (lifetime.IsCancellationRequested) { }
        }

        private async Task RunWebSocket()
        {
            try
            {
                while (true)
                {
                    var context = await listener!.GetContextAsync().WaitAsync(lifetime.Token);
                    using var socket = (await context.AcceptWebSocketAsync(null)).WebSocket;
                    if (textOnly)
                    {
                        await socket.SendAsync(Encoding.UTF8.GetBytes("unsupported telemetry"), WebSocketMessageType.Text, true, lifetime.Token);
                        await Task.Delay(Timeout.Infinite, lifetime.Token);
                        continue;
                    }
                    var parser = new MavLinkV2FrameParser(definitions);
                    var buffer = new byte[4096];
                    try
                    {
                        while (socket.State == WebSocketState.Open)
                        {
                            var packet = await socket.ReceiveAsync(buffer.AsMemory(), lifetime.Token);
                            if (packet.MessageType == WebSocketMessageType.Close)
                            {
                                break;
                            }
                            var replies = Reply(parser.Parse(buffer.AsSpan(0, packet.Count), new("ws", "fixture"), DateTimeOffset.UtcNow)).ToArray();
                            if (replies.Length == 0)
                            {
                                continue;
                            }
                            var bytes = new byte[] { 1, 2, 3 }.Concat(replies.SelectMany(x => x)).ToArray();
                            await socket.SendAsync(bytes.AsMemory(0, 7), WebSocketMessageType.Binary, false, lifetime.Token);
                            await socket.SendAsync(bytes.AsMemory(7, 6), WebSocketMessageType.Binary, true, lifetime.Token);
                            await socket.SendAsync(bytes.AsMemory(13), WebSocketMessageType.Binary, true, lifetime.Token);
                        }
                    }
                    catch (WebSocketException) { }
                }
            }
            catch (Exception) when (lifetime.IsCancellationRequested) { }
        }

        private IEnumerable<byte[]> Reply(IReadOnlyList<MavLinkFrame> frames)
        {
            foreach (var frame in frames)
            {
                var payload = frame.Payload.ToArray();
                if (frame.MessageId == 0)
                {
                    Interlocked.Increment(ref GcsHeartbeats);
                    Assert.Equal(6, payload[4]);
                    foreach (byte id in new byte[] { 41, 42 })
                    {
                        yield return Frame(id, 0, new byte[] { 0, 0, 0, 0, 2, 3, 128, 4, 3 }, 50);
                        var attitude = new byte[28];
                        BinaryPrimitives.WriteSingleLittleEndian(attitude.AsSpan(4), 0.25f);
                        yield return Frame(id, 30, attitude, 39);
                    }
                }
                else if (frame.MessageId == 21)
                {
                    var parameter = new byte[25];
                    BinaryPrimitives.WriteSingleLittleEndian(parameter, payload[0]);
                    BinaryPrimitives.WriteUInt16LittleEndian(parameter.AsSpan(4), 1);
                    Encoding.ASCII.GetBytes("TEST_VALUE").CopyTo(parameter, 8);
                    parameter[24] = 9;
                    yield return Frame(payload[0], 22, parameter, 220);
                }
                else if (frame.MessageId == 76)
                {
                    var command = BinaryPrimitives.ReadUInt16LittleEndian(payload.AsSpan(28));
                    Commands.Add((payload[30], command));
                    var ack = new byte[3];
                    BinaryPrimitives.WriteUInt16LittleEndian(ack, command);
                    yield return Frame(payload[30], 77, ack, 143);
                }
            }
        }

        private byte[] Frame(byte id, byte message, byte[] payload, byte extra)
        {
            var bytes = new byte[payload.Length + 12];
            bytes[0] = 0xfd;
            bytes[1] = (byte)payload.Length;
            bytes[4] = sequence++;
            bytes[5] = id;
            bytes[6] = 1;
            bytes[7] = message;
            payload.CopyTo(bytes, 10);
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(bytes.Length - 2), MavLinkCrc.Calculate(bytes.AsSpan(1, payload.Length + 9), extra));
            return bytes;
        }

        public async ValueTask DisposeAsync()
        {
            await lifetime.CancelAsync();
            listener?.Close();
            udp?.Dispose();
            await worker;
            lifetime.Dispose();
        }
    }
}
