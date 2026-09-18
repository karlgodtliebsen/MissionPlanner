using System.Buffers.Binary;
using Microsoft.Extensions.Logging;
using MissionPlanner.Library.Logging;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.MavLink.Services;
using MissionPlanner.MavLink.Services.Abstractions;

namespace MissionPlanner.Core.Replay;

/// <summary>Records received raw frames as classic Mission Planner tlogs, independently of UI lifetime.</summary>
/// <param name="storage">Platform log storage.</param>
/// <param name="events">Application recording-status event hub.</param>
/// <param name="logger">Recording diagnostics.</param>
public sealed class TelemetryRecordingService(
    ILogStorage storage,
    IDomainEventHub events,
    ILogger<TelemetryRecordingService> logger) : IMavLinkTrafficRecording
{
    private long latestSession;
    private TelemetryRecordingStatus current = new("Idle", null, null);

    /// <summary>Gets recording state for the latest connection.</summary>
    public TelemetryRecordingStatus Current => Volatile.Read(ref current);

    /// <inheritdoc />
    public IAsyncDisposable Start(MavLinkInspectionTap tap)
    {
        var session = Interlocked.Increment(ref latestSession);
        try
        {
            var lease = tap.Subscribe(4096);
            var started = DateTimeOffset.UtcNow;
            Update(session, new("Recording", null, null) { Started = started });
            return new Recording(storage, lease, status => Update(session, status), started);
        }
        catch (Exception exception)
        {
            Update(session, new("Error", null, exception.Message));
            logger.LogWarning(exception, "PC telemetry recording could not start.");
            return EmptyRecording.Instance;
        }
    }

    private void Update(long session, TelemetryRecordingStatus status)
    {
        if (session != Volatile.Read(ref latestSession))
        {
            return;
        }

        Volatile.Write(ref current, status);
        _ = PublishAsync(status);
    }

    private async Task PublishAsync(TelemetryRecordingStatus status)
    {
        try
        {
            await events.PublishDomainEventAsync(new TelemetryRecordingChanged(status)).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "PC recording status notification failed.");
        }
    }

    private sealed class Recording : IAsyncDisposable
    {
        private readonly ILogStorage storage;
        private readonly MavLinkInspectionLease lease;
        private readonly Action<TelemetryRecordingStatus> update;
        private readonly DateTimeOffset started;
        private readonly CancellationTokenSource cancellation = new();
        private readonly Task writer;
        private string? name;
        private long bytes;
        private int disposed;

        public Recording(ILogStorage storage, MavLinkInspectionLease lease, Action<TelemetryRecordingStatus> update, DateTimeOffset started)
        {
            this.storage = storage;
            this.lease = lease;
            this.update = update;
            this.started = started;
            writer = Task.Run(WriteAsync);
        }

        private TelemetryRecordingStatus Status(string state, string? error = null)
            => new(state, name, error, lease.Dropped) { Started = started, BytesWritten = bytes };

        private async Task<Stream> CreateAsync()
        {
            var stem = started.ToString("yyyy-MM-dd HH-mm-ss", System.Globalization.CultureInfo.InvariantCulture);
            for (var suffix = 0; suffix < 10000; suffix++)
            {
                name = suffix == 0 ? $"{stem}.tlog" : $"{stem}-{suffix}.tlog";
                try
                {
                    return await storage.CreateAsync(LogStorageArea.Telemetry, name, cancellation.Token).ConfigureAwait(false);
                }
                catch (IOException)
                {
                    // Retry only name collisions; permission and quota errors must remain visible.
                    var existing = await storage.ListAsync(LogStorageArea.Telemetry, cancellation.Token).ConfigureAwait(false);
                    if (!existing.Any(item => item.Id == name))
                    {
                        throw;
                    }
                }
            }

            throw new IOException("Unable to allocate a unique telemetry log name.");
        }

        private async Task WriteAsync()
        {
            var timestamp = new byte[8];
            Stream? stream = null;
            string? error = null;
            try
            {
                while (await lease.Reader.WaitToReadAsync(cancellation.Token).ConfigureAwait(false))
                {
                    while (lease.Reader.TryRead(out var observation))
                    {
                        if (observation.Direction != MavLinkTrafficDirection.Inbound)
                        {
                            continue;
                        }

                        stream ??= await CreateAsync().ConfigureAwait(false);
                        var microseconds = checked((ulong)((observation.Frame.ReceivedAt - DateTimeOffset.UnixEpoch).Ticks / 10));
                        BinaryPrimitives.WriteUInt64BigEndian(timestamp, microseconds);
                        await stream.WriteAsync(timestamp, cancellation.Token).ConfigureAwait(false);
                        await stream.WriteAsync(observation.Frame.RawBytes, cancellation.Token).ConfigureAwait(false);
                        bytes += timestamp.Length + observation.Frame.RawBytes.Length;
                    }

                    if (stream is not null)
                    {
                        await stream.FlushAsync(cancellation.Token).ConfigureAwait(false);
                    }

                    update(Status(lease.Dropped == 0 ? "Recording" : "Error",
                        lease.Dropped == 0 ? null : "Recording incomplete: traffic exceeded the recording queue."));
                }

                if (stream is not null)
                {
                    await stream.FlushAsync(cancellation.Token).ConfigureAwait(false);
                }
            }
            catch (Exception exception)
            {
                error = exception.Message;
            }
            finally
            {
                lease.Dispose();
                if (stream is not null)
                {
                    try
                    {
                        await stream.DisposeAsync().ConfigureAwait(false);
                    }
                    catch (Exception exception)
                    {
                        error ??= exception.Message;
                    }
                }

                error ??= lease.Dropped == 0 ? null : "Recording incomplete: traffic exceeded the recording queue.";
                update(Status(error is null ? "Completed" : "Error", error));
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref disposed, 1) != 0)
            {
                return;
            }

            lease.Complete();
            cancellation.CancelAfter(TimeSpan.FromSeconds(5));
            await writer.ConfigureAwait(false);
            cancellation.Dispose();
        }
    }

    private sealed class EmptyRecording : IAsyncDisposable
    {
        public static EmptyRecording Instance { get; } = new();
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
