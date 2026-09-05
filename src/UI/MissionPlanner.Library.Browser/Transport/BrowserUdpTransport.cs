using System.Buffers.Binary;
using System.Net;
using System.Net.WebSockets;
using MissionPlanner.Transport;
using MissionPlanner.Transport.Abstractions;

namespace MissionPlanner.Library.Browser.Transport;

/// <summary>One WebSocket message per UDP datagram, through the local bridge.</summary>
public sealed class BrowserUdpTransport : IUdpMavLinkTransport
{
    private readonly ClientWebSocket socket;
    private readonly Uri bridgeUri;
    public BrowserUdpTransport(Uri bridgeUri) : this(bridgeUri, new ClientWebSocket()) { }
    internal BrowserUdpTransport(Uri bridgeUri, ClientWebSocket socket)
    {
        this.bridgeUri = bridgeUri;
        this.socket = socket;
    }
    private readonly SemaphoreSlim sendLock = new(1, 1);
    private readonly byte[] packet = new byte[65513];
    private int offset;
    private int remaining;
    private TransportEndPoint? peer;

    public bool IsConnected => socket.State == WebSocketState.Open;

    public async Task ConnectAsync(CancellationToken cancellationToken)
    {
        if (IsConnected) return;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        try { await socket.ConnectAsync(bridgeUri, timeout.Token); }
        catch (Exception ex) when (ex is WebSocketException or OperationCanceledException)
        {
            socket.Abort();
            cancellationToken.ThrowIfCancellationRequested();
            throw new IOException("Cannot connect to the local UDP bridge. Start MissionPlanner.BrowserBridge, open the app from that host, and check that UDP port 14550 is available.", ex);
        }
    }

    public async ValueTask<TransportReceiveResult> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        if (buffer.IsEmpty) throw new ArgumentException("A receive buffer is required.", nameof(buffer));
        if (remaining == 0)
        {
            var count = 0;
            ValueWebSocketReceiveResult result;
            do
            {
                result = await socket.ReceiveAsync(packet.AsMemory(count), cancellationToken);
                if (result.MessageType == WebSocketMessageType.Close)
                    throw new IOException("The UDP bridge closed the connection.");
                if (result.MessageType != WebSocketMessageType.Binary)
                    throw new IOException("The UDP bridge sent a non-binary message.");
                count += result.Count;
                if (count == packet.Length && !result.EndOfMessage)
                    throw new IOException("The UDP bridge datagram exceeds the size limit.");
            } while (!result.EndOfMessage);
            if (count <= 6) throw new IOException("The UDP bridge sent an invalid datagram.");
            peer = new TransportEndPoint("udp", new IPEndPoint(new IPAddress(packet.AsSpan(0, 4)),
                BinaryPrimitives.ReadUInt16BigEndian(packet.AsSpan(4, 2))));
            offset = 6;
            remaining = count - 6;
        }
        var copied = System.Math.Min(buffer.Length, remaining);
        packet.AsMemory(offset, copied).CopyTo(buffer);
        offset += copied;
        remaining -= copied;
        return new TransportReceiveResult(copied, peer);
    }

    public async ValueTask WriteAsync(ReadOnlyMemory<byte> data, TransportEndPoint endPoint, CancellationToken cancellationToken)
    {
        if (data.IsEmpty || data.Length > 65507) throw new ArgumentOutOfRangeException(nameof(data));
        // The native bridge pins one UDP peer; it never accepts a destination
        // address from the browser. MAVLink identity/routing remains shared.
        await sendLock.WaitAsync(cancellationToken);
        try { await socket.SendAsync(data, WebSocketMessageType.Binary, true, cancellationToken); }
        finally { sendLock.Release(); }
    }

    public Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        socket.Abort(); // Also releases a pending read without waiting for a peer.
        remaining = 0;
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        socket.Abort();
        socket.Dispose();
        return ValueTask.CompletedTask;
    }
}
