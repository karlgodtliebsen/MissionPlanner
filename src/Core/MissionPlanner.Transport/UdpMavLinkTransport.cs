using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MissionPlanner.Transport;

/// <inheritdoc />
public sealed class UdpMavLinkTransport : IMavLinkTransport
{
    private readonly ILogger<UdpMavLinkTransport> logger;
    private UdpClient? udpClient;
    private readonly TransportEndpoint endpoint;
    private volatile bool isConnected;

    /// <summary>
    /// 
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    /// <exception cref="ArgumentException"></exception>
    public UdpMavLinkTransport(IOptions<TransportEndpoint> options, ILogger<UdpMavLinkTransport> logger)
    {
        this.logger = logger;
        endpoint = options.Value;

        var remoteHost = endpoint.RemoteHost;
        var remotePort = endpoint.RemotePort;
        var localPort = endpoint.LocalPort;

        if (localPort is <= 0 or > 65535) throw new ArgumentOutOfRangeException(nameof(localPort));

        if (remotePort is <= 0 or > 65535) throw new ArgumentOutOfRangeException(nameof(remotePort));

        if (string.IsNullOrWhiteSpace(remoteHost)) throw new ArgumentException("Remote host must be specified.", nameof(remoteHost));
    }

    /// <inheritdoc />
    public bool IsConnected => isConnected;

    /// <inheritdoc />
    public Task ConnectAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var localPort = endpoint.LocalPort;
        var localHost = endpoint.LocalHost;

        var localAddress = string.IsNullOrWhiteSpace(localHost)
            ? IPAddress.Any
            : IPAddress.Parse(localHost);
        udpClient = new UdpClient(new IPEndPoint(localAddress, localPort));
        isConnected = true;
        logger.LogTrace("UdpMavLinkTransport - UDP transport connected to host: {localHost} on port: {localPort}", localHost, localPort);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async ValueTask<TransportReceiveResult> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        if (!isConnected) throw new InvalidOperationException("UDP transport is not connected.");

        if (udpClient is null) throw new InvalidOperationException("UDP Client is not initialized.");

        var result = await udpClient.ReceiveAsync(cancellationToken).ConfigureAwait(false);
        var bytesToCopy = Math.Min(result.Buffer.Length, buffer.Length);
        result.Buffer.AsMemory(0, bytesToCopy).CopyTo(buffer);

        var remoteEndpoint = new MavLinkEndpoint(endpoint.Protocol, result.RemoteEndPoint.Address.ToString(), result.RemoteEndPoint.Port);
        logger.LogTrace("UdpMavLinkTransport - Received {Bytes} bytes from {RemoteEndPoint}", bytesToCopy, remoteEndpoint);
        return new TransportReceiveResult(bytesToCopy, remoteEndpoint);
    }

    /// <inheritdoc />
    public async ValueTask WriteAsync(ReadOnlyMemory<byte> data, IPEndPoint ipEndpoint, CancellationToken cancellationToken)
    {
        if (!isConnected) throw new InvalidOperationException("UDP transport is not connected.");

        if (udpClient is null) throw new InvalidOperationException("UDP Client is not initialized.");

        logger.LogTrace("UdpMavLinkTransport - UDP sending {Length} bytes to {RemoteAddress}:{RemotePort}", data.Length, ipEndpoint.Address, ipEndpoint.Port);

        await udpClient.SendAsync(data, ipEndpoint, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        isConnected = false;
        if (udpClient is not null)
        {
            udpClient.Close();
            udpClient.Dispose();
            udpClient = null!;
        }

        var localPort = endpoint.LocalPort;
        var localHost = endpoint.LocalHost;
        logger.LogTrace("UdpMavLinkTransport - UDP transport disconnected from host: {localHost} on port: {localPort}", localHost, localPort);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        isConnected = false;
        if (udpClient is not null)
        {
            udpClient.Close();
            udpClient.Dispose();
            udpClient = null!;
        }

        logger.LogTrace("UdpMavLinkTransport - UDP transport disposed");
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }
}