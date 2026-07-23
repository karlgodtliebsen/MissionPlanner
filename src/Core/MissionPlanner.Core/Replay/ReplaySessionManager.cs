using Microsoft.Extensions.Logging;
using MissionPlanner.MavLink.Services.Abstractions;

namespace MissionPlanner.Core.Replay;

/// <summary>Uses cancellable wall-clock delays for production replay timing.</summary>
public sealed class ReplayDelay : IReplayDelay
{
    /// <inheritdoc />
    public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken) =>
        delay <= TimeSpan.Zero ? Task.CompletedTask : Task.Delay(delay, cancellationToken);
}

/// <summary>Coordinates indexed, speed-adjusted telemetry-log playback in a read-only state pipeline.</summary>
public sealed class ReplaySessionManager : IReplaySessionManager
{
    private readonly ITelemetryLogReader reader;
    private readonly IReplayTelemetryPipeline pipeline;
    private readonly IReplayDelay delay;
    private readonly ILogger<ReplaySessionManager> logger;
    private readonly SemaphoreSlim operationGate = new(1, 1);
    private readonly object stateLock = new();
    private ReplaySessionSnapshot snapshot = ReplaySessionSnapshot.Unloaded;
    private Stream? stream;
    private CancellationTokenSource? playbackCancellation;
    private Task? playbackTask;
    private bool disposed;

    /// <summary>Initializes the replay session coordinator.</summary>
    /// <param name="reader">Telemetry-log index and random-access reader.</param>
    /// <param name="pipeline">Replay-only decoding and state pipeline.</param>
    /// <param name="delay">Replay timing abstraction.</param>
    /// <param name="logger">Structured workflow logger.</param>
    public ReplaySessionManager(
        ITelemetryLogReader reader,
        IReplayTelemetryPipeline pipeline,
        IReplayDelay delay,
        ILogger<ReplaySessionManager> logger)
    {
        this.reader = reader;
        this.pipeline = pipeline;
        this.delay = delay;
        this.logger = logger;
    }

    /// <inheritdoc />
    public ReplaySessionSnapshot Snapshot
    {
        get
        {
            lock (stateLock)
            {
                return snapshot;
            }
        }
    }

    /// <inheritdoc />
    public ReplayClockSnapshot? Current => Snapshot.Clock;

    /// <inheritdoc />
    public event EventHandler<ReplaySessionChangedEventArgs>? Changed;

    /// <inheritdoc />
    public async Task<ReplaySessionSnapshot> LoadAsync(
        Stream stream,
        string sourceName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ObjectDisposedException.ThrowIf(disposed, this);
        await operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await StopPlaybackCoreAsync().ConfigureAwait(false);
            this.stream?.Dispose();
            this.stream = null;
            pipeline.Reset();
            var sessionId = Guid.NewGuid();
            Publish(new ReplaySessionSnapshot(
                sessionId,
                ReplaySessionState.Indexing,
                null,
                0,
                null,
                [],
                0,
                0,
                $"Indexing telemetry log '{sourceName}'.",
                null));

            try
            {
                var index = await reader.IndexAsync(stream, sourceName, cancellationToken).ConfigureAwait(false);
                this.stream = stream;
                var firstTime = index.StartedAt ?? DateTimeOffset.UnixEpoch;
                logger.LogInformation(
                    "Indexed replay session {SessionId} from {SourceName} with {FrameCount} frame(s) and duration {Duration}.",
                    sessionId,
                    index.SourceName,
                    index.Entries.Count,
                    index.Duration);
                return Publish(Snapshot with
                {
                    State = ReplaySessionState.Ready,
                    Index = index,
                    Clock = new ReplayClockSnapshot(firstTime, TimeSpan.Zero, index.Duration, 1, false),
                    Message = index.Entries.Count == 0
                        ? "The telemetry log is empty. Replay is read-only until it is closed."
                        : $"Replay ready: {index.Entries.Count} frames. Outbound transmission is disabled."
                });
            }
            catch (Exception exception)
            {
                stream.Dispose();
                logger.LogError(exception, "Failed to index replay session {SessionId} from {SourceName}.", sessionId, sourceName);
                Publish(Snapshot with
                {
                    State = ReplaySessionState.Failed,
                    Message = "Telemetry-log indexing failed. Outbound transmission remains disabled until the replay is closed.",
                    Failure = exception.Message
                });
                throw;
            }
        }
        finally
        {
            operationGate.Release();
        }
    }

    /// <inheritdoc />
    public async Task<ReplaySessionSnapshot> PlayAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        await operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var current = Snapshot;
            if (current.Index is null || stream is null)
            {
                throw new InvalidOperationException("Load a telemetry log before starting replay.");
            }

            if (current.State == ReplaySessionState.Playing)
            {
                return current;
            }

            await StopPlaybackCoreAsync().ConfigureAwait(false);
            current = Snapshot;

            if (current.Index.Entries.Count == 0)
            {
                return Publish(current with
                {
                    State = ReplaySessionState.Completed,
                    Message = "Replay completed; the telemetry log contains no frames."
                });
            }

            if (current.NextFrameIndex >= current.Index.Entries.Count)
            {
                await SeekCoreAsync(TimeSpan.Zero, cancellationToken).ConfigureAwait(false);
                current = Snapshot;
            }

            playbackCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            current = Publish(current with
            {
                State = ReplaySessionState.Playing,
                Clock = current.Clock! with { IsRunning = true },
                Message = "Replay is playing. Outbound transmission is disabled.",
                Failure = null
            });
            playbackTask = Task.Run(() => PlaybackLoopAsync(playbackCancellation.Token), CancellationToken.None);
            return current;
        }
        finally
        {
            operationGate.Release();
        }
    }

    /// <inheritdoc />
    public async Task<ReplaySessionSnapshot> PauseAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        await operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (Snapshot.State != ReplaySessionState.Playing)
            {
                return Snapshot;
            }

            await StopPlaybackCoreAsync().ConfigureAwait(false);
            var current = Snapshot;
            return Publish(current with
            {
                State = ReplaySessionState.Paused,
                Clock = current.Clock is null ? null : current.Clock with { IsRunning = false },
                Message = "Replay is paused. Outbound transmission remains disabled."
            });
        }
        finally
        {
            operationGate.Release();
        }
    }

    /// <inheritdoc />
    public async Task<ReplaySessionSnapshot> SeekAsync(
        TimeSpan position,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        await operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await StopPlaybackCoreAsync().ConfigureAwait(false);
            return await SeekCoreAsync(position, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            operationGate.Release();
        }
    }

    /// <inheritdoc />
    public ReplaySessionSnapshot SetSpeed(double speed)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (!double.IsFinite(speed) || speed is < 0.1 or > 50)
        {
            throw new ArgumentOutOfRangeException(nameof(speed), "Replay speed must be between 0.1 and 50.");
        }

        var current = Snapshot;
        if (current.Clock is null)
        {
            throw new InvalidOperationException("Load a telemetry log before changing replay speed.");
        }

        return Publish(current with { Clock = current.Clock with { Speed = speed } });
    }

    /// <inheritdoc />
    public async Task CloseAsync(CancellationToken cancellationToken = default)
    {
        if (disposed)
        {
            return;
        }

        await operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await StopPlaybackCoreAsync().ConfigureAwait(false);
            stream?.Dispose();
            stream = null;
            pipeline.Reset();
            Publish(ReplaySessionSnapshot.Unloaded);
            logger.LogInformation("Closed the telemetry-log replay and re-enabled outbound transmission.");
        }
        finally
        {
            operationGate.Release();
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        await CloseAsync().ConfigureAwait(false);
        disposed = true;
        operationGate.Dispose();
    }

    private async Task PlaybackLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var current = Snapshot;
                var index = current.Index ?? throw new InvalidOperationException("Replay index was released during playback.");
                var ownedStream = stream ?? throw new InvalidOperationException("Replay stream was released during playback.");
                if (current.NextFrameIndex >= index.Entries.Count)
                {
                    Publish(current with
                    {
                        State = ReplaySessionState.Completed,
                        Clock = current.Clock is null ? null : current.Clock with { IsRunning = false },
                        Message = "Replay completed. Outbound transmission remains disabled until the log is closed."
                    });
                    return;
                }

                var entry = index.Entries[current.NextFrameIndex];
                var record = await reader.ReadAsync(ownedStream, entry, cancellationToken).ConfigureAwait(false);
                var decoded = await pipeline.ProcessAsync(record.Packet, entry.Timestamp, cancellationToken).ConfigureAwait(false);
                var elapsed = index.StartedAt is { } start ? entry.Timestamp - start : TimeSpan.Zero;
                current = Snapshot;
                current = Publish(current with
                {
                    NextFrameIndex = current.NextFrameIndex + 1,
                    Clock = new ReplayClockSnapshot(entry.Timestamp, elapsed, index.Duration, current.Clock?.Speed ?? 1, true),
                    Vehicles = pipeline.Vehicles,
                    DecodedFrames = current.DecodedFrames + (decoded ? 1 : 0),
                    RejectedFrames = current.RejectedFrames + (decoded ? 0 : 1)
                });

                if (current.NextFrameIndex >= index.Entries.Count)
                {
                    continue;
                }

                var recordedDelay = index.Entries[current.NextFrameIndex].Timestamp - entry.Timestamp;
                var speed = current.Clock?.Speed ?? 1;
                var wallDelay = recordedDelay <= TimeSpan.Zero
                    ? TimeSpan.Zero
                    : TimeSpan.FromTicks((long)(recordedDelay.Ticks / speed));
                await delay.DelayAsync(wallDelay, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Replay session {SessionId} failed at frame {FrameIndex}.", Snapshot.SessionId, Snapshot.NextFrameIndex);
            var current = Snapshot;
            Publish(current with
            {
                State = ReplaySessionState.Failed,
                Clock = current.Clock is null ? null : current.Clock with { IsRunning = false },
                Message = "Telemetry-log replay failed. Outbound transmission remains disabled until the log is closed.",
                Failure = exception.Message
            });
        }
    }

    private async Task<ReplaySessionSnapshot> SeekCoreAsync(TimeSpan position, CancellationToken cancellationToken)
    {
        var current = Snapshot;
        var index = current.Index ?? throw new InvalidOperationException("Load a telemetry log before seeking.");
        var ownedStream = stream ?? throw new InvalidOperationException("The loaded telemetry-log stream is unavailable.");
        var bounded = position < TimeSpan.Zero ? TimeSpan.Zero : position > index.Duration ? index.Duration : position;
        var targetTime = (index.StartedAt ?? DateTimeOffset.UnixEpoch) + bounded;
        var targetIndex = LowerBound(index.Entries, targetTime);
        pipeline.Reset();
        long decoded = 0;
        long rejected = 0;
        for (var frameIndex = 0; frameIndex < targetIndex; frameIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var entry = index.Entries[frameIndex];
            var record = await reader.ReadAsync(ownedStream, entry, cancellationToken).ConfigureAwait(false);
            if (await pipeline.ProcessAsync(record.Packet, entry.Timestamp, cancellationToken).ConfigureAwait(false))
            {
                decoded++;
            }
            else
            {
                rejected++;
            }
        }

        var actualTime = targetIndex == index.Entries.Count && index.EndedAt is { } end
            ? end
            : targetTime;
        return Publish(current with
        {
            State = ReplaySessionState.Paused,
            NextFrameIndex = targetIndex,
            Clock = new ReplayClockSnapshot(actualTime, bounded, index.Duration, current.Clock?.Speed ?? 1, false),
            Vehicles = pipeline.Vehicles,
            DecodedFrames = decoded,
            RejectedFrames = rejected,
            Message = $"Replay seeked to {bounded:g}. Outbound transmission remains disabled.",
            Failure = null
        });
    }

    private async Task StopPlaybackCoreAsync()
    {
        var cancellation = playbackCancellation;
        var task = playbackTask;
        playbackCancellation = null;
        playbackTask = null;
        if (cancellation is null)
        {
            return;
        }

        await cancellation.CancelAsync().ConfigureAwait(false);
        if (task is not null)
        {
            try
            {
                await task.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        }

        cancellation.Dispose();
    }

    private ReplaySessionSnapshot Publish(ReplaySessionSnapshot next)
    {
        lock (stateLock)
        {
            snapshot = next;
        }

        Changed?.Invoke(this, new ReplaySessionChangedEventArgs(next));
        return next;
    }

    private static int LowerBound(IReadOnlyList<TelemetryLogIndexEntry> entries, DateTimeOffset target)
    {
        var low = 0;
        var high = entries.Count;
        while (low < high)
        {
            var middle = low + ((high - low) / 2);
            if (entries[middle].Timestamp < target)
            {
                low = middle + 1;
            }
            else
            {
                high = middle;
            }
        }

        return low;
    }
}

/// <summary>Blocks every outbound MAVLink connection while a read-only replay is loaded.</summary>
public sealed class ReplayTransmissionPolicy(IReplaySessionManager replaySessionManager) : IMavLinkTransmissionPolicy
{
    /// <inheritdoc />
    public void ThrowIfTransmissionProhibited()
    {
        if (replaySessionManager.Snapshot.IsTransmissionProhibited)
        {
            throw new MavLinkTransmissionProhibitedException(
                "Outbound MAVLink transmission is disabled while telemetry-log replay is active. Close the replay before sending to a live or simulated vehicle.");
        }
    }
}
