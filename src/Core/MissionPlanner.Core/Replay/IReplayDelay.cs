namespace MissionPlanner.Core.Replay;

/// <summary>Abstracts replay timing so deterministic tests never wait on wall-clock time.</summary>
public interface IReplayDelay
{
    /// <summary>Waits for one speed-adjusted interval.</summary>
    /// <param name="delay">Non-negative wall-clock delay.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken);
}
