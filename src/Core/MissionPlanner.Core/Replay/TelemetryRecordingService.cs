using System.Buffers.Binary;
using Microsoft.Extensions.Logging;
using MissionPlanner.Core.ConfigTuning.Planner;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.MavLink.Services;
using MissionPlanner.MavLink.Services.Abstractions;

namespace MissionPlanner.Core.Replay;

/// <summary>Automatically records connection traffic as timestamped Mission Planner-compatible tlog frames.</summary>
/// <param name="settings">Persisted Planner logging-directory preference.</param>
/// <param name="events">Application recording-status event hub.</param>
/// <param name="logger">Recording diagnostics.</param>
public sealed class TelemetryRecordingService(
    IPlannerSettingsService settings,
    IDomainEventHub events,
    ILogger<TelemetryRecordingService> logger) : IMavLinkTrafficRecording
{
    private long latestSession;
    private TelemetryRecordingStatus current = new("Idle", null, null);

    /// <summary>Gets recording state for the latest connection, including finalized file location.</summary>
    public TelemetryRecordingStatus Current => Volatile.Read(ref current);

    /// <inheritdoc />
    public IAsyncDisposable Start(MavLinkInspectionTap tap)
    {
        var session = Interlocked.Increment(ref latestSession);
        FileStream? stream = null;
        MavLinkInspectionLease? lease = null;
        string? path = null;
        try
        {
            var directory = settings.Current.Logging.LogDirectory;
            if (string.IsNullOrWhiteSpace(directory))
            {
                directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MissionPlanner", "Telemetry");
            }
            directory = Path.GetFullPath(directory);
            Directory.CreateDirectory(directory);
            var stem = DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss-ffffff", System.Globalization.CultureInfo.InvariantCulture);
            for (var suffix = 0; ; suffix++)
            {
                path = Path.Combine(directory, $"{stem}-{suffix:D3}.tlog");
                try
                {
                    stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read, 65536, FileOptions.Asynchronous);
                    break;
                }
                catch (IOException) when (File.Exists(path) && suffix < 999)
                {
                }
            }
            lease = tap.Subscribe(4096);
            Update(session, new("Recording", path, null));
            return new Recording(stream, lease, status => Update(session, status), path);
        }
        catch (Exception exception)
        {
            lease?.Dispose();
            stream?.Dispose();
            Update(session, new("Error", path, exception.Message));
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
        private readonly FileStream stream;
        private readonly MavLinkInspectionLease lease;
        private readonly Action<TelemetryRecordingStatus> update;
        private readonly string path;
        private readonly CancellationTokenSource cancellation = new();
        private readonly Task writer;
        private int disposed;

        public Recording(FileStream stream, MavLinkInspectionLease lease, Action<TelemetryRecordingStatus> update, string path)
        {
            this.stream = stream;
            this.lease = lease;
            this.update = update;
            this.path = path;
            writer = Task.Run(WriteAsync);
        }

        private async Task WriteAsync()
        {
            var timestamp = new byte[8];
            long reportedDrops = 0;
            try
            {
                while (await lease.Reader.WaitToReadAsync(cancellation.Token).ConfigureAwait(false))
                {
                    while (lease.Reader.TryRead(out var observation))
                    {
                        var microseconds = checked((ulong)((observation.Frame.ReceivedAt - DateTimeOffset.UnixEpoch).Ticks / 10));
                        BinaryPrimitives.WriteUInt64BigEndian(timestamp, microseconds);
                        await stream.WriteAsync(timestamp, cancellation.Token).ConfigureAwait(false);
                        await stream.WriteAsync(observation.Frame.RawBytes, cancellation.Token).ConfigureAwait(false);
                    }
                    await stream.FlushAsync(cancellation.Token).ConfigureAwait(false);
                    if (lease.Dropped != reportedDrops)
                    {
                        reportedDrops = lease.Dropped;
                        update(new("Error", path, "Recording incomplete: traffic exceeded the recording queue.", reportedDrops));
                    }
                }
                await stream.FlushAsync(cancellation.Token).ConfigureAwait(false);
                update(lease.Dropped == 0
                    ? new("Completed", path, null)
                    : new("Error", path, "Recording incomplete: traffic exceeded the recording queue.", lease.Dropped));
            }
            catch (Exception exception)
            {
                update(new("Error", path, exception.Message, lease.Dropped));
            }
            finally
            {
                lease.Dispose();
                try
                {
                    await stream.DisposeAsync().ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    update(new("Error", path, exception.Message, lease.Dropped));
                }
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
