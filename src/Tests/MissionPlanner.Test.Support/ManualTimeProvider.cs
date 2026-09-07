namespace MissionPlanner.Test.Support;

/// <summary>A deterministic monotonic clock with coalesced timers, advanced explicitly by tests.</summary>
public sealed class ManualTimeProvider(DateTimeOffset initial) : TimeProvider
{
    private readonly object sync = new();
    private readonly HashSet<ManualTimer> timers = [];
    private long ticks;
    /// <inheritdoc />
    public override long TimestampFrequency => TimeSpan.TicksPerSecond;
    /// <inheritdoc />
    public override long GetTimestamp() { lock (sync) { return ticks; } }
    /// <inheritdoc />
    public override DateTimeOffset GetUtcNow() { lock (sync) { return initial.ToUniversalTime().AddTicks(ticks); } }
    /// <summary>Gets the number of live timer handles.</summary>
    public int TimerCount { get { lock (sync) { return timers.Count; } } }
    /// <inheritdoc />
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new ManualTimer(this, callback, state);
        timer.Change(dueTime, period);
        return timer;
    }
    /// <summary>Advances monotonically and fires each elapsed timer once; overdue periodic ticks coalesce.</summary>
    public void Advance(TimeSpan elapsed)
    {
        if (elapsed < TimeSpan.Zero) { throw new ArgumentOutOfRangeException(nameof(elapsed)); }
        List<ManualTimer> ready = [];
        lock (sync)
        {
            ticks = checked(ticks + elapsed.Ticks);
            foreach (var timer in timers)
            {
                if (timer.Due > ticks) { continue; }
                ready.Add(timer);
                timer.Due = timer.Period > 0 ? ticks + timer.Period : long.MaxValue;
            }
        }
        foreach (var timer in ready) { timer.Fire(); }
    }

    private sealed class ManualTimer(ManualTimeProvider clock, TimerCallback callback, object? state) : ITimer
    {
        private bool disposed;
        public long Due { get; set; }
        public long Period { get; private set; }
        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            lock (clock.sync)
            {
                if (disposed) { return false; }
                Due = dueTime == Timeout.InfiniteTimeSpan ? long.MaxValue : clock.ticks + dueTime.Ticks;
                Period = period.Ticks;
                clock.timers.Add(this);
                return true;
            }
        }
        public void Fire()
        {
            lock (clock.sync) { if (disposed) { return; } }
            callback(state);
        }
        public void Dispose() { lock (clock.sync) { disposed = true; clock.timers.Remove(this); } }
        public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
    }
}
