using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MissionPlanner.Library;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.MavLink.Client;
using MissionPlanner.MavLink.Messages;
using MissionPlanner.Transport;

namespace MissionPlanner.MavLink.Services;

/// <summary>
/// Owns the MAVLink processing pipeline: received byte blocks -> frames -> decoded messages -> event hub.
/// The serial receive loop is intentionally not blocked by frame decoding or event subscribers.
/// </summary>
public sealed class MavLinkConnection : IMavLinkConnection, IAsyncDisposable
{
    private readonly IMavLinkClient client;
    private readonly IMavLinkFrameParser frameParser;
    private readonly IMavLinkMessageDecodeHandler messageDecoder;
    private readonly IEventHub eventHub;
    private readonly ILogger<MavLinkConnection> logger;
    private readonly MavLinkConnectionPipelineOptions options;
    private readonly SemaphoreSlim lifecycleLock = new(1, 1);
    private Channel<DecodedMavLinkMessage>? decodedMessages;

    private CancellationTokenSource? cancellationTokenSource;
    private Task? parseTask;
    private Task? publishTask;
    private bool disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="MavLinkConnection"/> class.
    /// </summary>
    /// <param name="client">The MAVLink client.</param>
    /// <param name="frameParser">The frame parser.</param>
    /// <param name="messageDecoder">The message decoder.</param>
    /// <param name="eventHub">The event hub.</param>
    /// <param name="options">The MAVLink connection pipeline options.</param>
    /// <param name="logger">The logger.</param>
    /// <exception cref="ArgumentNullException"></exception>
    public MavLinkConnection(
        IMavLinkClient client,
        IMavLinkFrameParser frameParser,
        IMavLinkMessageDecodeHandler messageDecoder,
        IEventHub eventHub,
        IOptions<MavLinkConnectionPipelineOptions> options,
        ILogger<MavLinkConnection> logger)
    {
        this.client = client ?? throw new ArgumentNullException(nameof(client));
        this.frameParser = frameParser ?? throw new ArgumentNullException(nameof(frameParser));
        this.messageDecoder = messageDecoder ?? throw new ArgumentNullException(nameof(messageDecoder));
        this.eventHub = eventHub ?? throw new ArgumentNullException(nameof(eventHub));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this.options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        this.options.Validate();
    }


    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        await lifecycleLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (cancellationTokenSource is not null)
            {
                return;
            }

            cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            decodedMessages = Channel.CreateBounded<DecodedMavLinkMessage>(new BoundedChannelOptions(options.DecodedMessageChannelCapacity) { SingleWriter = true, SingleReader = true, FullMode = BoundedChannelFullMode.Wait, AllowSynchronousContinuations = false });

            await client.StartAsync(cancellationTokenSource.Token).ConfigureAwait(false);

            parseTask = Task.Run(() => ParseLoopAsync(cancellationTokenSource.Token), CancellationToken.None);
            publishTask = Task.Run(() => PublishLoopAsync(cancellationTokenSource.Token), CancellationToken.None);

            if (logger.IsEnabled(LogLevel.Debug))
            {
                logger.LogDebug("MAVLink connection pipeline started.");
            }
        }
        finally
        {
            lifecycleLock.Release();
        }
    }

    /// <inheritdoc />
    public async ValueTask SendRawAsync(ReadOnlyMemory<byte> data, TransportEndPoint endPoint, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await client.SendAsync(data, endPoint, cancellationToken).ConfigureAwait(false);
    }

    private async Task ParseLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var received in client.ReceivedBytes.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                using (received)
                {
                    IReadOnlyList<MavLinkFrame> frames;
                    DomainException.ThrowIfNull(received.RemoteEndpoint);
                    try
                    {
                        frames = frameParser.Parse(received.Span, received.RemoteEndpoint, received.ReceivedAt);
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Failed to parse MAVLink byte block with {ByteCount} bytes.", received.Length);
                        continue;
                    }

                    foreach (var frame in frames)
                    {
                        if (!messageDecoder.TryDecode(frame, out var message) || message is null)
                        {
                            if (logger.IsEnabled(LogLevel.Warning))
                            {
                                logger.LogWarning("Failed to decode MAVLink frame. MessageId={MessageId}, SystemId={SystemId}, ComponentId={ComponentId}",
                                    frame.MessageId,
                                    frame.SystemId,
                                    frame.ComponentId);
                            }

                            continue;
                        }

                        await decodedMessages!.Writer.WriteAsync(new DecodedMavLinkMessage(message, received.ReceivedAt), cancellationToken).ConfigureAwait(false);

                        if (logger.IsEnabled(LogLevel.Trace))
                        {
                            logger.LogTrace("Decoded MAVLink message {MessageType}.", message.GetType().Name);
                        }
                    }
                }
            }

            decodedMessages?.Writer.TryComplete();
        }
        catch (OperationCanceledException ex)
        {
            decodedMessages?.Writer.TryComplete(ex);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected exception in MAVLink parse loop.");
            decodedMessages?.Writer.TryComplete(ex);
        }
    }

    private async Task PublishLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var decoded in decodedMessages!.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                if (decoded.Message is MavLinkMessage message)
                {
                    await eventHub.PublishAsync<MavLinkMessage>(MavLinkEventTopics.ReceivedMessage, message, cancellationToken).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown path.
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected exception in MAVLink publish loop.");
            throw;
        }
    }

    /// <summary>
    /// Stops the MAVLink connection and associated tasks.
    /// </summary>
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
            await client.StopAsync().ConfigureAwait(false);

            if (parseTask is not null)
            {
                try
                {
                    await parseTask.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // Normal shutdown path.
                }
            }

            if (publishTask is not null)
            {
                try
                {
                    await publishTask.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // Normal shutdown path.
                }
            }

            decodedMessages?.Writer.TryComplete();
            decodedMessages = null;
            cancellationTokenSource.Dispose();
            cancellationTokenSource = null;
            parseTask = null;
            publishTask = null;
        }
        finally
        {
            lifecycleLock.Release();
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        await StopAsync().ConfigureAwait(false);
        await client.DisposeAsync().ConfigureAwait(false);
        disposed = true;
        GC.SuppressFinalize(this);
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
    }
}
