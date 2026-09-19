using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MissionPlanner.Library;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.MavLink.Client;
using MissionPlanner.MavLink.Messages;
using MissionPlanner.MavLink.Services.Abstractions;
using MissionPlanner.Transport;

namespace MissionPlanner.MavLink.Services;

/// <summary>
/// Owns the MAVLink processing pipeline: received byte blocks -> frames -> decoded messages -> event hub.
/// The serial receive loop is intentionally not blocked by frame decoding or event subscribers.
/// </summary>
public sealed class MavLinkConnection : IMavLinkConnection
{
    private readonly IMavLinkClient client;
    private readonly IMavLinkFrameParser frameParser;
    private readonly IMavLinkMessageDecodeHandler messageDecoder;
    private readonly IMavLinkTransmissionPolicy? transmissionPolicy;
    private readonly IEventHub eventHub;
    private readonly ILogger<MavLinkConnection> logger;
    private readonly MavLinkConnectionPipelineOptions options;
    private readonly SemaphoreSlim lifecycleLock = new(1, 1);
    private Channel<DecodedMavLinkMessage>? decodedMessages;

    private CancellationTokenSource? cancellationTokenSource;
    private Task? receiveCompletionTask;
    private Task? parseTask;
    private Task? publishTask;
    private bool disposed;
    private bool inspectionAttached;
    private readonly IMavLinkTrafficRecording? trafficRecording;
    private IAsyncDisposable? recording;
    private readonly string connectionId = Guid.NewGuid().ToString("N");
    private readonly MavLinkInspectionTap recordingTap = new();

    /// <inheritdoc />
    public MavLinkInspectionTap? Inspection { get; }

    /// <inheritdoc />
    public MavLinkConnectionActivity Activity { get; }
    /// <inheritdoc />
    public MissionPlanner.MavLink.Signing.MavLinkSigningSession? Signing { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="MavLinkConnection"/> class.
    /// </summary>
    /// <param name="client">The MAVLink client.</param>
    /// <param name="frameParser">The frame parser.</param>
    /// <param name="messageDecoder">The message decoder.</param>
    /// <param name="eventHub">The event hub.</param>
    /// <param name="options">The MAVLink connection pipeline options.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="transmissionPolicy">Optional application safety policy for outbound frames.</param>
    /// <param name="inspection">Optional connection-owned bounded traffic observers.</param>
    /// <param name="signing">Optional connection-owned signing and verification.</param>
    /// <param name="clock">Monotonic receipt clock, shared with connection monitoring.</param>
    /// <param name="trafficRecording">Optional connection-boundary telemetry recorder.</param>
    /// <exception cref="ArgumentNullException"></exception>
    public MavLinkConnection(
        IMavLinkClient client,
        IMavLinkFrameParser frameParser,
        IMavLinkMessageDecodeHandler messageDecoder,
        IEventHub eventHub,
        IOptions<MavLinkConnectionPipelineOptions> options,
        ILogger<MavLinkConnection> logger,
        IMavLinkTransmissionPolicy? transmissionPolicy = null,
        MavLinkInspectionTap? inspection = null,
        MissionPlanner.MavLink.Signing.MavLinkSigningSession? signing = null, IMavLinkTrafficRecording? trafficRecording = null, TimeProvider? clock = null)
    {
        this.client = client ?? throw new ArgumentNullException(nameof(client));
        this.frameParser = frameParser ?? throw new ArgumentNullException(nameof(frameParser));
        this.messageDecoder = messageDecoder ?? throw new ArgumentNullException(nameof(messageDecoder));
        this.eventHub = eventHub ?? throw new ArgumentNullException(nameof(eventHub));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this.transmissionPolicy = transmissionPolicy;
        Inspection = inspection ?? (trafficRecording is null ? null : new MavLinkInspectionTap());
        this.trafficRecording = trafficRecording;
        Activity = new MavLinkConnectionActivity(clock ?? TimeProvider.System);
        Signing = signing;
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

            frameParser.Reset();
            if (!inspectionAttached && frameParser is MavLinkV2FrameParser inspectable)
            {
                inspectable.UnknownFrameObserved += UnknownFrameObserved;
                inspectionAttached = true;
            }
            cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            decodedMessages = Channel.CreateBounded<DecodedMavLinkMessage>(new BoundedChannelOptions(options.DecodedMessageChannelCapacity) { SingleWriter = true, SingleReader = true, FullMode = BoundedChannelFullMode.Wait, AllowSynchronousContinuations = false });

            if (Inspection is not null)
            {
                recording = trafficRecording?.Start(recordingTap);
            }
            try
            {
                await client.StartAsync(cancellationTokenSource.Token).ConfigureAwait(false);
            }
            catch
            {
                if (recording is not null)
                {
                    await recording.DisposeAsync().ConfigureAwait(false);
                    recording = null;
                }
                throw;
            }

            receiveCompletionTask = ObserveReceiveCompletionAsync();
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
        transmissionPolicy?.ThrowIfTransmissionProhibited();
        var original = data;
        try
        {
            if (Signing is not null)
            {
                data = await Signing.SignAsync(data, cancellationToken).ConfigureAwait(false);
            }
            await client.SendAsync(data, endPoint, cancellationToken).ConfigureAwait(false);
            if (Inspection?.HasObservers == true)
            {
                ObserveOutbound(data.Span, endPoint);
            }
        }
        finally
        {
            if (!data.Equals(original) && System.Runtime.InteropServices.MemoryMarshal.TryGetArray(data, out var owned))
            {
                System.Security.Cryptography.CryptographicOperations.ZeroMemory(owned.AsSpan());
            }
        }
    }

    private void ObserveOutbound(ReadOnlySpan<byte> bytes, TransportEndPoint endpoint)
    {
        // Read only framing metadata; do not run another decoder or alter transport data.
        while (bytes.Length >= 8 && bytes[0] is 0xFD or 0xFE)
        {
            var v2 = bytes[0] == 0xFD;
            var header = v2 ? 10 : 6;
            if (bytes.Length < header)
            {
                return;
            }
            var length = header + bytes[1] + 2 + (v2 && (bytes[2] & 1) != 0 ? 13 : 0);
            if (bytes.Length < length)
            {
                return;
            }
            var id = v2 ? (uint)(bytes[7] | bytes[8] << 8 | bytes[9] << 16) : bytes[5];
            if (id == 256)
            {
                bytes = bytes[length..];
                continue;
            }
            var raw = bytes[..length].ToArray();
            var frame = new MavLinkFrame(raw[v2 ? 5 : 3], raw[v2 ? 6 : 4], endpoint, id,
                raw[v2 ? 4 : 2], raw.AsMemory(header, raw[1]), raw, DateTimeOffset.UtcNow);
            Inspection!.Publish(new(MavLinkTrafficDirection.Outbound, frame, null, false));
            bytes = bytes[length..];
        }
    }

    private void UnknownFrameObserved(MavLinkFrame frame)
    {
        // Unsupported dialect frames retain their original bytes for future decoders.
        if (frame.MessageId != 256 && recordingTap.HasObservers)
        {
            recordingTap.Publish(new(MavLinkTrafficDirection.Inbound, frame, null, false));
        }
        if (Inspection?.HasObservers == true)
        {
            Inspection.Publish(new(MavLinkTrafficDirection.Inbound, frame, null, false));
        }
    }

    private async Task ObserveReceiveCompletionAsync()
    {
        if (client.Completion is not { } completion)
        {
            return;
        }
        try
        {
            await completion.ConfigureAwait(false);
        }
        catch (Exception)
        {
            Activity.End("TransportFault");
            return;
        }
        Activity.End(client.ReceiveFailure is null ? "TransportClosed" : "TransportFault");
    }

    private async Task ParseLoopAsync(CancellationToken cancellationToken)
    {
        using var logScope = logger.BeginScope(new Dictionary<string, object> { ["ConnectionId"] = connectionId });
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
                        // SETUP_SIGNING contains a secret key, never publish it into diagnostic or domain channels.
                        if (frame.MessageId == 256)
                        {
                            continue;
                        }
                        var signature = Signing?.Verify(frame.RawBytes.Span)
                            ?? MissionPlanner.MavLink.Signing.MavLinkSignatureStatus.Unverified;
                        if (recordingTap.HasObservers && signature is not
                            (MissionPlanner.MavLink.Signing.MavLinkSignatureStatus.Invalid or
                             MissionPlanner.MavLink.Signing.MavLinkSignatureStatus.Replay))
                        {
                            recordingTap.Publish(new(MavLinkTrafficDirection.Inbound, frame, null, true) { Signature = signature });
                        }
                        if (signature is not (MissionPlanner.MavLink.Signing.MavLinkSignatureStatus.Invalid or MissionPlanner.MavLink.Signing.MavLinkSignatureStatus.Replay))
                        {
                            Activity.ReportValidFrame(frame.SystemId, frame.MessageId == 0, received.ReceivedAt);
                        }
                        var decoded = messageDecoder.TryDecode(frame, out var message);
                        if (Inspection?.HasObservers == true)
                        {
                            Inspection.Publish(new(MavLinkTrafficDirection.Inbound, frame, message, true) { Signature = signature });
                        }
                        if (signature is MissionPlanner.MavLink.Signing.MavLinkSignatureStatus.Invalid
                            or MissionPlanner.MavLink.Signing.MavLinkSignatureStatus.Replay)
                        {
                            continue;
                        }
                        if (!decoded || message is null)
                        {
                            if (logger.IsEnabled(LogLevel.Warning))
                            {
                                logger.LogWarning("Failed to decode MAVLink frame. MessageId={MessageId}, SystemId={SystemId}, ComponentId={ComponentId}", frame.MessageId, frame.SystemId, frame.ComponentId);
                            }

                            continue;
                        }

                        await decodedMessages!.Writer.WriteAsync(new DecodedMavLinkMessage(message, received.ReceivedAt), cancellationToken).ConfigureAwait(false);

                        if (logger.IsEnabled(LogLevel.Trace))
                        {
                            logger.LogTrace("Decoded {MessageType}. MavLinkMessageId={MavLinkMessageId}, SystemId={SystemId}, ComponentId={ComponentId}, Transport={Transport}",
                                message.GetType().Name, frame.MessageId, frame.SystemId, frame.ComponentId, frame.EndPoint.TransportName);
                        }
                    }
                }
            }

            decodedMessages?.Writer.TryComplete();
            Activity.End("TransportClosed");
        }
        catch (OperationCanceledException ex)
        {
            decodedMessages?.Writer.TryComplete(ex);
        }
        catch (Exception ex)
        {
            Activity.End("TransportFault");
            logger.LogError(ex, "Unexpected exception in MAVLink parse loop.");
            decodedMessages?.Writer.TryComplete(ex);
        }
    }

    private async Task PublishLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var decoded in decodedMessages!.Reader.ReadAllAsync(cancellationToken))
            {
                if (decoded.Message is MavLinkMessage message)
                {
                    await eventHub.PublishAsync<MavLinkMessage>(MavLinkEventTopics.ReceivedMessage, message, cancellationToken);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown path.
        }
        catch (Exception ex)
        {
            Activity.End("PipelineFault");
            logger.LogError(ex, "Unexpected exception in MAVLink publish loop.");
            throw;
        }
    }

    /// <summary>
    /// Stops the MAVLink connection and associated tasks.
    /// </summary>
    public async Task StopAsync()
    {
        await lifecycleLock.WaitAsync();
        try
        {

            Signing?.Dispose();
            if (inspectionAttached && frameParser is MavLinkV2FrameParser inspectable)
            {
                inspectable.UnknownFrameObserved -= UnknownFrameObserved;
                inspectionAttached = false;
            }
            if (cancellationTokenSource is null)
            {
                return;
            }

            Activity.End("UserRequested");
            await cancellationTokenSource.CancelAsync();
            await client.StopAsync();
            if (receiveCompletionTask is not null)
            {
                await receiveCompletionTask.ConfigureAwait(false);
            }

            if (parseTask is not null)
            {
                try
                {
                    await parseTask;
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
                    await publishTask;
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
            try
            {
                if (recording is not null)
                {
                    await recording.DisposeAsync().ConfigureAwait(false);
                    recording = null;
                }
                Inspection?.CloseObservers();
            }
            finally
            {
                lifecycleLock.Release();
            }
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
        Signing?.Dispose();
        await client.DisposeAsync().ConfigureAwait(false);
        disposed = true;
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
    }
}
