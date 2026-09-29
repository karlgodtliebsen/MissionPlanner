using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MissionPlanner.Transport.Abstractions;

namespace MissionPlanner.Transport;

/// <summary>One UDP socket for listening or communication with a fixed remote peer.</summary>
public sealed class UdpMavLinkTransport : IUdpMavLinkTransport
{
    private readonly TransportEndpoint endpoint;
    private readonly ILogger<UdpMavLinkTransport> logger;
    private UdpClient? client;
    private IPEndPoint? peer;
    private byte[]? pending;
    private int pendingOffset;
    private IPEndPoint? pendingSource;

    /// <summary>Creates a UDP transport with explicit listening or client semantics.</summary>
    public UdpMavLinkTransport(IOptions<TransportEndpoint> options, ILogger<UdpMavLinkTransport> logger)
    {
        endpoint = options.Value;
        this.logger = logger;
        if (endpoint.LocalPort < (endpoint.IsUdpClient ? 0 : 1) || endpoint.LocalPort > 65535)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Invalid local UDP port.");
        }
        if (endpoint.IsUdpClient && (endpoint.RemotePort is < 1 or > 65535 || string.IsNullOrWhiteSpace(endpoint.RemoteHost)))
        {
            throw new ArgumentException("UDP Client requires a remote host and port.", nameof(options));
        }
    }

    /// <summary>Gets the resolved client peer after opening, or null for a listener.</summary>
    public TransportEndPoint? RemotePeer => peer is null ? null : new("udp", peer);

    /// <inheritdoc />
    public bool IsConnected => client is not null;

    /// <inheritdoc />
    public async Task ConnectAsync(CancellationToken cancellationToken)
    {
        if (client is not null)
        {
            return;
        }
        var bind = string.IsNullOrWhiteSpace(endpoint.LocalHost) ? null : IPAddress.Parse(endpoint.LocalHost);
        IPEndPoint? remote = null;
        if (endpoint.IsUdpClient)
        {
            var addresses = await Dns.GetHostAddressesAsync(endpoint.RemoteHost, cancellationToken).ConfigureAwait(false);
            var address = addresses.OrderBy(a => a.AddressFamily == AddressFamily.InterNetwork ? 0 : 1)
                .FirstOrDefault(a => bind is null || a.AddressFamily == bind.AddressFamily)
                ?? throw new IOException("The remote host has no address matching the local interface.");
            remote = new(address, endpoint.RemotePort);
        }
        bind ??= remote?.AddressFamily == AddressFamily.InterNetworkV6 ? IPAddress.IPv6Any : IPAddress.Any;
        var socket = new UdpClient(new IPEndPoint(bind, endpoint.LocalPort));
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (remote is not null)
            {
                // Connected UDP filters incoming datagrams to this address and port.
                socket.Connect(remote);
            }
            peer = remote;
            pending = null;
            pendingOffset = 0;
            client = socket;
            logger.LogInformation("UDP socket ready on {LocalEndpoint}; awaiting MAVLink traffic", socket.Client.LocalEndPoint);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }

    /// <inheritdoc />
    public async ValueTask<TransportReceiveResult> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        if (buffer.IsEmpty)
        {
            throw new ArgumentException("A non-empty receive buffer is required.", nameof(buffer));
        }
        var socket = client ?? throw new IOException("UDP socket is closed.");
        while (pending is null)
        {
            var datagram = await socket.ReceiveAsync(cancellationToken).ConfigureAwait(false);
            if (datagram.Buffer.Length == 0)
            {
                continue;
            }
            // Retain the remainder rather than truncating large datagrams.
            pending = datagram.Buffer;
            pendingOffset = 0;
            pendingSource = datagram.RemoteEndPoint;
        }
        var count = Math.Min(buffer.Length, pending.Length - pendingOffset);
        pending.AsMemory(pendingOffset, count).CopyTo(buffer);
        pendingOffset += count;
        var source = new TransportEndPoint("udp", pendingSource!);
        if (pendingOffset == pending.Length)
        {
            pending = null;
        }
        return new(count, source);
    }

    /// <inheritdoc />
    public async ValueTask WriteAsync(ReadOnlyMemory<byte> data, TransportEndPoint endPoint, CancellationToken cancellationToken)
    {
        var socket = client ?? throw new IOException("UDP socket is closed.");
        if (peer is not null)
        {
            if (!peer.Equals(endPoint.ToIPEndPoint()))
            {
                throw new IOException("The requested destination differs from the configured UDP peer.");
            }
            await socket.SendAsync(data, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await socket.SendAsync(data, endPoint.ToIPEndPoint(), cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        Interlocked.Exchange(ref client, null)?.Dispose();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync() => await DisconnectAsync().ConfigureAwait(false);
}
