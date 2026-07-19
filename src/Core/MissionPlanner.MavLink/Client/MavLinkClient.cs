using System.Buffers;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MissionPlanner.Library.DateTime.Domain;
using MissionPlanner.Transport;
using MissionPlanner.Transport.Abstractions;

namespace MissionPlanner.MavLink.Client;

/// <summary>
/// Transport-facing MAVLink client. It's receive loop only reads bytes and enqueues rented buffers.
/// Parsing, decoding, and event publishing are intentionally done elsewhere.
/// </summary>
public sealed class MavLinkClient : IMavLinkClient
{
    private readonly IMavLinkTransport transport;
    private readonly IDateTimeProvider dateTimeProvider;
    private readonly ILogger<MavLinkClient> logger;
    private readonly MavLinkClientPipelineOptions options;
    private readonly MemoryPool<byte> memoryPool;
    private readonly SemaphoreSlim lifecycleLock = new(1, 1);
    private Channel<PooledMavLinkDataReceived> receivedBytes;

    private CancellationTokenSource? cancellationTokenSource;
    private Task? receiveTask;
    private bool disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="MavLinkClient"/> class.
    /// </summary>
    /// <param name="transport">The MAVLink transport.</param>
    /// <param name="options">The MAVLink client pipeline options.</param>
    /// <param name="dateTimeProvider">The date time provider.</param>
    /// <param name="logger">The logger.</param>
    /// <exception cref="ArgumentNullException"></exception>
    public MavLinkClient(
        IMavLinkTransport transport,
        IOptions<MavLinkClientPipelineOptions> options,
        IDateTimeProvider dateTimeProvider,
        ILogger<MavLinkClient> logger)
    {
        this.transport = transport ?? throw new ArgumentNullException(nameof(transport));
        this.dateTimeProvider = dateTimeProvider ?? throw new ArgumentNullException(nameof(dateTimeProvider));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this.options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        this.options.Validate();
        memoryPool = MemoryPool<byte>.Shared;
        receivedBytes = CreateReceiveChannel();
    }

    /// <summary>
    /// Gets the channel reader for the received MAVLink data.
    /// </summary>
    public ChannelReader<PooledMavLinkDataReceived> ReceivedBytes => receivedBytes.Reader;

    /// <summary>
    /// Gets a value indicating whether the MAVLink client is running.
    /// </summary>  
    public bool IsRunning => receiveTask is { IsCompleted: false };

    /// <summary>
    /// Gets a value indicating whether the MAVLink client is connected.
    /// </summary>  
    public bool IsConnected => transport.IsConnected;


    /// <inheritdoc/>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        await lifecycleLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (IsRunning)
            {
                return;
            }

            cancellationTokenSource?.Dispose();
            cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            receivedBytes = CreateReceiveChannel();

            await transport.ConnectAsync(cancellationTokenSource.Token).ConfigureAwait(false);
            receiveTask = Task.Run(() => ReceiveLoopAsync(cancellationTokenSource.Token), CancellationToken.None);

            if (logger.IsEnabled(LogLevel.Debug))
            {
                logger.LogDebug("MAVLink client started. ReceiveBufferSize={ReceiveBufferSize}, ChannelCapacity={ChannelCapacity}",
                    options.ReceiveBufferSize,
                    options.ReceiveChannelCapacity);
            }
        }
        finally
        {
            lifecycleLock.Release();
        }
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested && transport.IsConnected)
            {
                var owner = memoryPool.Rent(options.ReceiveBufferSize);
                PooledMavLinkDataReceived? received = null;

                try
                {
                    var result = await transport.ReadAsync(owner.Memory[..options.ReceiveBufferSize], cancellationToken).ConfigureAwait(false);

                    if (result.BytesRead <= 0)
                    {
                        owner.Dispose();
                        continue;
                    }

                    received = new PooledMavLinkDataReceived(owner, result.BytesRead, result.RemoteEndpoint, dateTimeProvider.UtcNow);
                    owner = null!; // ownership transferred to received

                    await receivedBytes.Writer.WriteAsync(received, cancellationToken).ConfigureAwait(false);
                    received = null; // ownership transferred to channel consumer

                    if (logger.IsEnabled(LogLevel.Trace))
                    {
                        logger.LogTrace("Read and queued {BytesRead} MAVLink bytes.", result.BytesRead);
                    }
                }
                catch
                {
                    received?.Dispose();
                    owner?.Dispose();
                    throw;
                }
            }

            receivedBytes.Writer.TryComplete();
        }
        catch (OperationCanceledException ex)
        {
            receivedBytes.Writer.TryComplete(ex);
        }
        catch (ObjectDisposedException ex)
        {
            receivedBytes.Writer.TryComplete(ex);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected exception in MAVLink receive loop.");
            receivedBytes.Writer.TryComplete(ex);
        }
        finally
        {
            await transport.DisconnectAsync(CancellationToken.None).ConfigureAwait(false);

            if (logger.IsEnabled(LogLevel.Debug))
            {
                logger.LogDebug("MAVLink receive loop stopped.");
            }
        }
    }

    /// <inheritdoc/>
    public async ValueTask SendAsync(ReadOnlyMemory<byte> data, TransportEndPoint endPoint, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        if (!transport.IsConnected)
        {
            throw new InvalidOperationException("Transport is not connected.");
        }

        await transport.WriteAsync(data, endPoint, cancellationToken).ConfigureAwait(false);

        if (logger.IsEnabled(LogLevel.Trace))
        {
            logger.LogTrace("Sent {Bytes} MAVLink bytes.", data.Length);
        }
    }

    /// <inheritdoc/>
    public async Task StopAsync()
    {
        await lifecycleLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (cancellationTokenSource is null)
            {
                return;
            }

            await cancellationTokenSource.CancelAsync().ConfigureAwait(false);

            if (receiveTask is not null)
            {
                try
                {
                    await receiveTask.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // Normal shutdown path.
                }
            }

            receivedBytes.Writer.TryComplete();
            await transport.DisposeAsync().ConfigureAwait(false);
            cancellationTokenSource.Dispose();
            cancellationTokenSource = null;
            receiveTask = null;
        }
        finally
        {
            lifecycleLock.Release();
        }
    }

    /// <inheritdoc/>
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
    }

    private Channel<PooledMavLinkDataReceived> CreateReceiveChannel()
    {
        return Channel.CreateBounded<PooledMavLinkDataReceived>(new BoundedChannelOptions(options.ReceiveChannelCapacity) { SingleWriter = true, SingleReader = true, FullMode = BoundedChannelFullMode.Wait, AllowSynchronousContinuations = false });
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
    }
}
