namespace MissionPlanner.Core.Replay;

/// <summary>Uses cancellable wall-clock delays for production replay timing.</summary>
public sealed class ReplayDelay : IReplayDelay
{
    /// <inheritdoc />
    public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        return delay <= TimeSpan.Zero ? Task.CompletedTask : Task.Delay(delay, cancellationToken);
    }
}
