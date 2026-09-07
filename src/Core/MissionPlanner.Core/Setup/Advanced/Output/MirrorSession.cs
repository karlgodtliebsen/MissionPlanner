using System.Security.Cryptography;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.MavLink.Services;

namespace MissionPlanner.Core.Setup.Advanced.Output;

/// <summary>Directions offered by the authoritative connection tap.</summary>
public enum MirrorDirection
{
    /// <summary>Only frames received from vehicles.</summary>
    Inbound,
    /// <summary>Only frames sent toward vehicles.</summary>
    Outbound,
    /// <summary>Both directions; reflected copies are suppressed.</summary>
    Both
}

/// <summary>A bounded, short-lived fingerprint cache suppressing reflected byte-identical frames.</summary>
public sealed class MirrorLoopGuard(TimeProvider clock)
{
    private readonly Dictionary<string, DateTimeOffset> recent = [];
    private readonly Queue<(string Hash, DateTimeOffset Expires)> expiry = [];
    /// <summary>Returns false for a recent identical frame; accepted frames are reserved before queueing.</summary>
    public bool Accept(ReadOnlySpan<byte> bytes)
    {
        var now = clock.GetUtcNow();
        while (expiry.TryPeek(out var entry) && (entry.Expires <= now || expiry.Count >= 4096))
        {
            expiry.Dequeue();
            recent.Remove(entry.Hash);
        }
        var hash = Convert.ToHexString(SHA256.HashData(bytes));
        if (recent.ContainsKey(hash)) { return false; }
        var expires = now.AddSeconds(30);
        recent[hash] = expires;
        expiry.Enqueue((hash, expires));
        return true;
    }
    /// <summary>Clears fingerprints at the session boundary.</summary>
    public void Clear() { recent.Clear(); expiry.Clear(); }
}

/// <summary>Forwards exact frames into the reusable output pump without decode/re-encode or vehicle writes.</summary>
public sealed class MirrorSession(IVehicleConnectionSession connection, IActiveVehicleContext vehicle,
    BoundedOutputSession output, TimeProvider clock)
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private CancellationTokenSource? cancellation;
    private MavLinkInspectionLease? observer;
    private Task reader = Task.CompletedTask;
    private long tapDrops;
    private long tapDroppedBytes;

    /// <summary>Gets output and observation-overflow counters.</summary>
    public OutputSessionSnapshot Snapshot()
    {
        var current = output.Snapshot();
        return current with { DroppedFrames = current.DroppedFrames + (observer?.Dropped ?? tapDrops),
            DroppedBytes = current.DroppedBytes + (observer?.DroppedBytes ?? tapDroppedBytes) };
    }

    /// <summary>Starts explicit forwarding. Both directions requires an acknowledged loop warning.</summary>
    public async Task StartAsync(OutputEndpoint endpoint, OutputSessionOptions options, MirrorDirection direction,
        bool loopWarningAccepted, CancellationToken token)
    {
        if (!Enum.IsDefined(direction)) { throw new ArgumentException("Select a valid mirror direction."); }
        if (direction == MirrorDirection.Both && !loopWarningAccepted) { throw new InvalidOperationException("Confirm the bidirectional loop warning first."); }
        await gate.WaitAsync(token).ConfigureAwait(false);
        var created = false;
        try
        {
            if (cancellation is not null) { throw new InvalidOperationException("Stop the mirror before restarting it."); }
            var tap = connection.Connection.Inspection ?? throw new NotSupportedException("This connection has no frame tap.");
            cancellation = CancellationTokenSource.CreateLinkedTokenSource(token, vehicle.ConnectionCancellationToken);
            created = true;
            await output.StartAsync(endpoint, options, cancellation.Token).ConfigureAwait(false);
            observer = tap.Subscribe(options.Capacity);
            tapDrops = tapDroppedBytes = 0;
            reader = ReadAsync(observer, direction, cancellation.Token);
        }
        catch
        {
            if (created)
            {
                cancellation?.Cancel();
                observer?.Dispose();
                observer = null;
                await output.StopAsync().ConfigureAwait(false);
                cancellation?.Dispose();
                cancellation = null;
            }
            throw;
        }
        finally { gate.Release(); }
    }

    /// <summary>Cancels and joins both observer and writer before returning.</summary>
    public async Task StopAsync()
    {
        cancellation?.Cancel();
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (cancellation is null) { return; }
            observer?.Dispose();
            await reader.ConfigureAwait(false);
            await output.StopAsync().ConfigureAwait(false);
            tapDrops = observer?.Dropped ?? 0;
            tapDroppedBytes = observer?.DroppedBytes ?? 0;
            observer = null;
            cancellation.Dispose();
            cancellation = null;
        }
        finally { gate.Release(); }
    }

    private async Task ReadAsync(MavLinkInspectionLease source, MirrorDirection direction, CancellationToken token)
    {
        using var reading = CancellationTokenSource.CreateLinkedTokenSource(token);
        var loopGuard = new MirrorLoopGuard(clock);
        async Task ConsumeAsync()
        {
            try
            {
                await foreach (var item in source.Reader.ReadAllAsync(reading.Token).ConfigureAwait(false))
                {
                    if (direction == MirrorDirection.Inbound && item.Direction != MavLinkTrafficDirection.Inbound
                        || direction == MirrorDirection.Outbound && item.Direction != MavLinkTrafficDirection.Outbound) { continue; }
                    // The tap already excludes secret-bearing SETUP_SIGNING packets.
                    if (!loopGuard.Accept(item.Frame.RawBytes.Span))
                    {
                        output.RecordDrop(item.Frame.RawBytes.Length);
                        continue;
                    }
                    output.Offer(item.Frame.RawBytes.Span);
                }
            }
            catch (OperationCanceledException) when (reading.IsCancellationRequested) { }
        }
        var consume = ConsumeAsync();
        try
        {
            await Task.WhenAny(consume, output.Completion).ConfigureAwait(false);
        }
        finally
        {
            reading.Cancel();
            source.Dispose();
            await consume.ConfigureAwait(false);
            await output.StopAsync().ConfigureAwait(false);
            loopGuard.Clear();
        }
    }
}
