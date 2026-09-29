using System.Net.WebSockets;
using Microsoft.Extensions.Options;
using MissionPlanner.Transport.Abstractions;

namespace MissionPlanner.Transport;

/// <summary>Raw binary MAVLink over WS/WSS, with serialized sends and no application queue.</summary>
public sealed class WebSocketMavLinkTransport : IWebSocketMavLinkTransport
{
    private readonly Uri uri;
    private readonly TransportEndPoint endpoint;
    private readonly SemaphoreSlim sendLock = new(1, 1);
    private ClientWebSocket? socket;

    /// <summary>Creates a WebSocket transport using the platform's TLS validation.</summary>
    public WebSocketMavLinkTransport(IOptions<TransportEndpoint> options)
    {
        if (!Uri.TryCreate(options.Value.WebSocketUrl, UriKind.Absolute, out var parsed)
            || parsed.Scheme is not ("ws" or "wss") || string.IsNullOrEmpty(parsed.Host)
            || !string.IsNullOrEmpty(parsed.UserInfo) || !string.IsNullOrEmpty(parsed.Fragment))
        {
            throw new ArgumentException("Enter a ws:// or wss:// URL without embedded credentials or a fragment.");
        }
        uri = parsed;
        // Paths and queries may contain secrets. Only retain the authority for display.
        endpoint = new(uri.Scheme, uri.GetLeftPart(UriPartial.Authority));
    }

    /// <summary>Gets a diagnostic-safe endpoint used for routing on this connection.</summary>
    public TransportEndPoint RemotePeer => endpoint;

    /// <inheritdoc />
    public bool IsConnected => socket?.State == WebSocketState.Open;

    /// <inheritdoc />
    public async Task ConnectAsync(CancellationToken cancellationToken)
    {
        if (IsConnected)
        {
            return;
        }
        var opened = new ClientWebSocket();
        try
        {
            await opened.ConnectAsync(uri, cancellationToken).ConfigureAwait(false);
            socket = opened;
        }
        catch (OperationCanceledException)
        {
            opened.Dispose();
            throw;
        }
        catch
        {
            opened.Dispose();
            // Inner exceptions can include the full request URL or server data.
            throw new IOException("WebSocket handshake failed. Check the endpoint, server availability, and TLS certificate trust.");
        }
    }

    /// <inheritdoc />
    public async ValueTask<TransportReceiveResult> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        var current = socket ?? throw new IOException("WebSocket is closed.");
        try
        {
            while (true)
            {
                var result = await current.ReceiveAsync(buffer, cancellationToken).ConfigureAwait(false);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    throw new IOException("The WebSocket server closed the connection.");
                }
                if (result.MessageType != WebSocketMessageType.Binary)
                {
                    throw new IOException("Unsupported WebSocket text payload. This connection requires raw binary MAVLink, not JSON or Socket.IO.");
                }
                if (result.Count > 0)
                {
                    // The MAVLink parser owns framing across chunks and message boundaries.
                    return new(result.Count, endpoint);
                }
            }
        }
        catch (WebSocketException)
        {
            throw new IOException("WebSocket receive failed or the server disconnected.");
        }
    }

    /// <inheritdoc />
    public async ValueTask WriteAsync(ReadOnlyMemory<byte> data, TransportEndPoint endPoint, CancellationToken cancellationToken)
    {
        var current = socket ?? throw new IOException("WebSocket is closed.");
        await sendLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!ReferenceEquals(current, socket) || current.State != WebSocketState.Open)
            {
                throw new IOException("WebSocket session ended before sending.");
            }
            await current.SendAsync(data, WebSocketMessageType.Binary, true, cancellationToken).ConfigureAwait(false);
        }
        catch (WebSocketException)
        {
            throw new IOException("WebSocket send failed or the server disconnected.");
        }
        finally
        {
            sendLock.Release();
        }
    }

    /// <inheritdoc />
    public Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        var previous = Interlocked.Exchange(ref socket, null);
        previous?.Abort();
        previous?.Dispose();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync() => await DisconnectAsync().ConfigureAwait(false);
}
