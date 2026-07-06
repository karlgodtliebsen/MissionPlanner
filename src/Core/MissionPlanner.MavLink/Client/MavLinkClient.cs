using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MissionPlanner.Library.DateTime.Domain;
using MissionPlanner.Transport;

namespace MissionPlanner.MavLink.Client;

/// <summary>
/// Represents a MAVLink client that can send and receive data from a MAVLink transport.
/// </summary>
public sealed class MavLinkClient : IMavLinkClient
{
    private readonly IMavLinkTransport transport;
    private readonly IDateTimeProvider dateTimeProvider;
    private readonly ILogger<MavLinkClient> logger;
    private readonly int receiveBufferSize;

    private CancellationTokenSource? cancellationTokenSource;
    private Task? receiveTask;
    private bool disposed;

    /// <summary>
    /// Occurs when data is received from the MAVLink transport.
    /// </summary>
    public event Func<MavLinkDataReceived, CancellationToken, Task>? DataReceived;

    /// <summary>
    /// Gets a value indicating whether the MAVLink client is currently running and receiving data.
    /// </summary>
    public bool IsRunning => receiveTask is { IsCompleted: false };

    /// <summary>
    /// Gets a value indicating whether the MAVLink client is connected to the transport.
    /// </summary>
    public bool IsConnected => transport.IsConnected;

    /// <summary>
    /// Initializes a new instance of the <see cref="MavLinkClient"/> class.
    /// </summary>
    /// <param name="transport">The MAVLink transport to use for communication.</param>
    /// <param name="options">The options for configuring the MAVLink client.</param>
    /// <param name="dateTimeProvider"></param>
    /// <param name="logger">The logger instance.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="transport"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="options"/> is null or contains invalid values.</exception>
    public MavLinkClient(IMavLinkTransport transport, IOptions<TransportEndpoint> options, IDateTimeProvider dateTimeProvider, ILogger<MavLinkClient> logger)
    {
        this.transport = transport ?? throw new ArgumentNullException(nameof(transport));
        this.dateTimeProvider = dateTimeProvider;
        this.logger = logger;
        receiveBufferSize = options.Value.ReceiveBufferSize;

        if (receiveBufferSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(receiveBufferSize), "Receive buffer size must be positive.");
        }
    }

    /// <summary>
    /// Starts the MAVLink client and begins receiving data.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (IsRunning)
        {
            return;
        }

        cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        await transport.ConnectAsync(cancellationTokenSource.Token).ConfigureAwait(false);

        receiveTask = Task.Run(() => ReceiveLoopAsync(cancellationTokenSource.Token), CancellationToken.None);
        logger.LogTrace("MAVLink client started.");
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[receiveBufferSize];

        try
        {
            while (!cancellationToken.IsCancellationRequested && transport.IsConnected)
            {
                var result = await transport.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);

                if (result.BytesRead <= 0)
                {
                    continue;
                }

                logger.LogTrace("MavLinkClient - Received {BytesRead} bytes from MAVLink transport.", result.BytesRead);
                var copy = new byte[result.BytesRead];
                buffer.AsMemory(0, result.BytesRead).CopyTo(copy);

                var received = new MavLinkDataReceived(copy, result.RemoteEndpoint, dateTimeProvider.UtcNow);

                var handler = DataReceived;

                if (handler is not null)
                {
                    await handler(received, cancellationToken).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown path.
        }
        catch (ObjectDisposedException)
        {
            // Normal when transport is closed while blocked in ReadAsync().
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "MavLinkClient - Unexpected exception in ReceiveLoop.");
            throw;
        }
        finally
        {
            await transport.DisconnectAsync(CancellationToken.None).ConfigureAwait(false);
            logger.LogTrace("MavLinkClient - MAVLink client stopped.");
        }
    }

    /// <summary>
    /// Sends data to the MAVLink transport.
    /// </summary>
    /// <param name="data">The data to send.</param>
    /// <param name="endPoint"></param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <exception cref="InvalidOperationException">Thrown if the transport is not connected.</exception>
    public async ValueTask SendAsync(ReadOnlyMemory<byte> data, TransportEndPoint endPoint, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (!transport.IsConnected)
        {
            throw new InvalidOperationException("Transport is not connected.");
        }

        await transport.WriteAsync(data, endPoint, cancellationToken).ConfigureAwait(false);
        logger.LogTrace("MavLinkClient - Sent {Bytes} bytes to MAVLink transport.", data.Length);
    }

    /// <summary>
    /// Stops the MAVLink client and cancels any ongoing operations.
    /// </summary>
    public async Task StopAsync()
    {
        if (cancellationTokenSource is null)
        {
            return;
        }

        await cancellationTokenSource.CancelAsync().ConfigureAwait(false);

        if (receiveTask is not null)
        {
            await receiveTask.ConfigureAwait(false);
        }

        cancellationTokenSource.Dispose();
        cancellationTokenSource = null;
        receiveTask = null;
        logger.LogTrace("MavLinkClient - MAVLink client stopped.");
    }

    /// <summary>
    /// Disposes the MAVLink client and releases all resources.
    /// </summary>
    /// <returns></returns>
    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        await StopAsync().ConfigureAwait(false);
        await transport.DisposeAsync().ConfigureAwait(false);

        disposed = true;
        GC.SuppressFinalize(this);
        logger.LogTrace("MavLinkClient - MAVLink client disposed.");
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
    }
}
