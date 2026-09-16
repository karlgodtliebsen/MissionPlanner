namespace MissionPlanner.Core.Replay;

/// <summary>Replays every indexed frame in order without wall-clock waiting for deterministic regression runs.</summary>
public sealed class ImmediateReplayDelay : IReplayDelay
{
    /// <inheritdoc />
    public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }
}
