using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;

namespace MissionPlanner.BrowserBridge;

/// <summary>A single-client, loopback-only UDP relay. It accepts no arbitrary destinations.</summary>
public sealed class UdpBridge(int port = 14550)
{
    private readonly SemaphoreSlim connection = new(1, 1);
    public int Port { get; } = port is > 0 and <= 65535 ? port : throw new ArgumentOutOfRangeException(nameof(port));

    public async Task HandleAsync(HttpContext context)
    {
        // Check both Host and Origin to prevent another website (or DNS rebinding)
        // from acquiring the local vehicle connection through the user's browser.
        var request = context.Request;
        if (request.Host.Host is not ("127.0.0.1" or "localhost") ||
            !IPAddress.IsLoopback(context.Connection.RemoteIpAddress ?? IPAddress.Any) ||
            !string.Equals(request.Headers.Origin.ToString(), $"{request.Scheme}://{request.Host}", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }
        if (!context.WebSockets.IsWebSocketRequest || request.Query["port"] != Port.ToString())
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }
        if (!await connection.WaitAsync(0, context.RequestAborted))
        {
            context.Response.StatusCode = StatusCodes.Status409Conflict;
            return;
        }
        try
        {
            using var udp = new UdpClient(AddressFamily.InterNetwork);
            udp.Client.ExclusiveAddressUse = true;
            try { udp.Client.Bind(new IPEndPoint(IPAddress.Loopback, Port)); }
            catch (SocketException)
            {
                context.Response.StatusCode = StatusCodes.Status409Conflict;
                await context.Response.WriteAsync("UDP port 14550 is already in use.");
                return;
            }
            using var socket = await context.WebSockets.AcceptWebSocketAsync();
            using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
            IPEndPoint? peer = null;
            var receive = ReceiveUdpAsync();
            var send = SendUdpAsync();
            try { await await Task.WhenAny(receive, send); }
            catch (Exception ex) when (ex is OperationCanceledException or WebSocketException or SocketException or IOException) { }
            finally
            {
                await lifetime.CancelAsync();
                socket.Abort();
                try { await Task.WhenAll(receive, send); }
                catch (Exception ex) when (ex is OperationCanceledException or WebSocketException or SocketException or IOException) { }
            }

            async Task ReceiveUdpAsync()
            {
                while (!lifetime.IsCancellationRequested)
                {
                    var packet = await udp.ReceiveAsync(lifetime.Token);
                    if (packet.Buffer.Length == 0) continue;
                    peer ??= packet.RemoteEndPoint;
                    if (!peer.Equals(packet.RemoteEndPoint)) continue;
                    var envelope = new byte[packet.Buffer.Length + 6];
                    peer.Address.GetAddressBytes().CopyTo(envelope, 0);
                    BinaryPrimitives.WriteUInt16BigEndian(envelope.AsSpan(4, 2), (ushort)peer.Port);
                    packet.Buffer.CopyTo(envelope, 6);
                    await socket.SendAsync(envelope.AsMemory(), WebSocketMessageType.Binary, true, lifetime.Token);
                }
            }

            async Task SendUdpAsync()
            {
                var buffer = new byte[65507];
                while (!lifetime.IsCancellationRequested)
                {
                    var count = 0;
                    ValueWebSocketReceiveResult result;
                    do
                    {
                        result = await socket.ReceiveAsync(buffer.AsMemory(count), lifetime.Token);
                        if (result.MessageType == WebSocketMessageType.Close) return;
                        if (result.MessageType != WebSocketMessageType.Binary) throw new IOException("Binary datagrams required.");
                        count += result.Count;
                        if (count == buffer.Length && !result.EndOfMessage) throw new IOException("Datagram too large.");
                    } while (!result.EndOfMessage);
                    if (count != 0 && peer is { } destination)
                        await udp.SendAsync(buffer.AsMemory(0, count), destination, lifetime.Token);
                }
            }
        }
        finally { connection.Release(); }
    }
}
