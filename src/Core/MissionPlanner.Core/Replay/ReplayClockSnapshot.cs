namespace MissionPlanner.Core.Replay;

/// <summary>Describes the deterministic replay clock.</summary>
/// <param name="LogTime">Current timestamp in the recorded log.</param>
/// <param name="Elapsed">Elapsed recorded time from the first frame.</param>
/// <param name="Duration">Total recorded duration.</param>
/// <param name="Speed">Playback speed multiplier.</param>
/// <param name="IsRunning">Whether the clock is currently advancing.</param>
public sealed record ReplayClockSnapshot(
    DateTimeOffset LogTime,
    TimeSpan Elapsed,
    TimeSpan Duration,
    double Speed,
    bool IsRunning);
