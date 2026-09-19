namespace MissionPlanner.MavLink.Services;

/// <summary>Allocation-free packet liveness and one-shot transport termination for one protocol connection.</summary>
public sealed class MavLinkConnectionActivity(TimeProvider clock)
{
    private readonly long[] packets = new long[256];
    private readonly long[] heartbeats = new long[256];
    private readonly long[] packetUtcTicks = new long[256];
    private readonly long[] heartbeatUtcTicks = new long[256];
    private readonly int[] seen = new int[256];
    private readonly TaskCompletionSource<string> ended = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly CancellationTokenSource operations = new();

    /// <summary>Completes once when transport/pipeline processing terminates.</summary>
    public Task<string> Ended => ended.Task;

    /// <summary>Shared cancellation boundary for requests owned by this connection.</summary>
    public CancellationToken LifetimeToken => operations.Token;

    /// <summary>Records a checksum/signature-accepted frame before message decoding.</summary>
    public void ReportValidFrame(byte systemId, bool heartbeat, DateTimeOffset receivedAt)
    {
        if (ended.Task.IsCompleted)
        {
            return;
        }
        Interlocked.Exchange(ref packets[systemId], clock.GetTimestamp());
        Interlocked.Exchange(ref packetUtcTicks[systemId], receivedAt.UtcTicks);
        if (heartbeat)
        {
            Interlocked.Exchange(ref heartbeats[systemId], clock.GetTimestamp());
            Interlocked.Exchange(ref heartbeatUtcTicks[systemId], receivedAt.UtcTicks);
        }
        Volatile.Write(ref seen[systemId], 1);
    }

    /// <summary>Returns packet age using monotonic time, independent of heartbeat and UTC adjustments.</summary>
    public TimeSpan? PacketAge(byte systemId) => Volatile.Read(ref seen[systemId]) == 0
        ? null : clock.GetElapsedTime(Interlocked.Read(ref packets[systemId]));

    /// <summary>Gets the last accepted packet's UTC receipt time for presentation.</summary>
    public DateTimeOffset? LastPacketAt(byte systemId) => ReadUtc(packetUtcTicks, systemId);

    /// <summary>Gets the independently tracked heartbeat receipt time.</summary>
    public DateTimeOffset? LastHeartbeatAt(byte systemId) => ReadUtc(heartbeatUtcTicks, systemId);

    /// <summary>Ends the connection once and immediately cancels in-flight request lifetimes.</summary>
    public void End(string reason)
    {
        if (ended.TrySetResult(reason))
        {
            operations.Cancel();
        }
    }

    private static DateTimeOffset? ReadUtc(long[] timestamps, byte systemId)
    {
        var value = Interlocked.Read(ref timestamps[systemId]);
        return value == 0 ? null : new DateTimeOffset(value, TimeSpan.Zero);
    }
}
