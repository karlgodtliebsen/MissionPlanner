using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MissionPlanner.Transport.Abstractions;
using Polly;
using Polly.Retry;

namespace MissionPlanner.Transport;

/// <summary>
/// MAVLink transport over TCP.
/// </summary>
public sealed class TcpMavLinkTransport : ITcpMavLinkTransport
{
    private readonly ILogger<TcpMavLinkTransport> logger;
    private readonly string remoteHost;
    private readonly int remotePort;
    private readonly TransportEndPoint remoteEndpoint;
    private readonly ResiliencePipeline retryPipeline;

    private TcpClient? tcpClient;
    private NetworkStream? stream;

    /// <summary>
    /// Initializes a new instance of the <see cref="TcpMavLinkTransport"/> class.
    /// </summary>
    /// <param name="options">The transport endpoint options.</param>
    /// <param name="logger">The logger instance.</param>
    /// <exception cref="ArgumentException">Thrown when the remote host is not specified.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the remote port is out of range.</exception>
    public TcpMavLinkTransport(IOptions<TransportEndpoint> options, ILogger<TcpMavLinkTransport> logger)
    {
        this.logger = logger;

        remoteHost = options.Value.RemoteHost;
        remotePort = options.Value.RemotePort;
        if (string.IsNullOrWhiteSpace(remoteHost))
        {
            throw new ArgumentException("Remote host must be specified.", nameof(remoteHost));
        }

        if (remotePort is <= 0 or > 65535)
        {
            throw new ArgumentOutOfRangeException(nameof(remotePort));
        }

        remoteEndpoint = new TransportEndPoint("tcp", remoteHost, remotePort);

        // Configure Polly retry pipeline for resilient TCP connection
        retryPipeline = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromMilliseconds(500),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                OnRetry = args =>
                {
                    logger.LogWarning(
                        args.Outcome.Exception,
                        "Retry attempt {AttemptNumber} of {MaxRetryAttempts} for TCP connection to {Host}:{Port}. Waiting {RetryDelay}ms before next attempt.",
                        args.AttemptNumber,
                        3,
                        remoteHost,
                        remotePort,
                        args.RetryDelay.TotalMilliseconds);
                    return ValueTask.CompletedTask;
                }
            })
            .Build();
    }

    /// <summary>
    /// Gets a value indicating whether the TCP transport is connected.
    /// </summary>
    public bool IsConnected => tcpClient?.Connected == true && stream is not null;

    /// <summary>
    /// Connects to the remote TCP endpoint.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task ConnectAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (IsConnected)
        {
            logger.LogTrace("TCP transport is already connected to {RemoteEndPoint}", remoteEndpoint);
            return;
        }

        // Use Polly retry mechanism to handle transient network errors when connecting
        await retryPipeline.ExecuteAsync(async ct =>
        {
            tcpClient = new TcpClient { NoDelay = true };
            await tcpClient.ConnectAsync(remoteHost, remotePort, ct).ConfigureAwait(false);
            stream = tcpClient.GetStream();

            logger.LogInformation("TCP transport connected successfully to {RemoteEndPoint}", remoteEndpoint);
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<TransportReceiveResult> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        if (!IsConnected || stream is null)
        {
            throw new InvalidOperationException("TCP transport is not connected.");
        }

        var bytesRead = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
        logger.LogTrace("TCP transport received {BytesRead} bytes from {RemoteEndPoint}", bytesRead, remoteEndpoint);
        return bytesRead == 0 ? throw new IOException("TCP connection was closed by the remote host.") : new TransportReceiveResult(bytesRead, remoteEndpoint);
    }

    /// <summary>
    /// Write MAVLink data to the remote ipEndpoint over TCP.
    /// </summary>
    /// <param name="data"></param>
    /// <param name="endPoint"> </param>
    /// <param name="cancellationToken"></param>
    /// <exception cref="InvalidOperationException"></exception>
    public async ValueTask WriteAsync(ReadOnlyMemory<byte> data, TransportEndPoint endPoint, CancellationToken cancellationToken)
    {
        if (!IsConnected || stream is null)
        {
            throw new InvalidOperationException("TCP transport is not connected.");
        }

        await stream.WriteAsync(data, cancellationToken).ConfigureAwait(false);

        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);

        logger.LogTrace("TCP transport sent {BytesSent} bytes to {RemoteEndPoint}", data.Length, remoteEndpoint);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        stream?.Dispose();
        stream = null;

        tcpClient?.Close();
        tcpClient?.Dispose();
        tcpClient = null;
        logger.LogTrace("TCP transport disconnected from {RemoteEndPoint}", remoteEndpoint);
        return Task.CompletedTask;
    }

    /// <summary>
    /// 
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync()
            .ConfigureAwait(false);

        GC.SuppressFinalize(this);
        logger.LogTrace("TCP transport disposed for {RemoteEndPoint}", remoteEndpoint);
    }
}
