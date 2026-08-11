namespace MissionPlanner.Core.Replay;

/// <summary>Contains a deterministic random-access index for one telemetry log.</summary>
/// <param name="SourceName">Display name supplied by the caller.</param>
/// <param name="Length">Indexed stream length in bytes.</param>
/// <param name="Entries">Ordered packet entries.</param>
/// <param name="StartedAt">First packet timestamp.</param>
/// <param name="EndedAt">Last packet timestamp.</param>
/// <param name="AdjustedTimestampCount">Number of backward timestamps clamped to preserve ordering.</param>
public sealed record TelemetryLogIndex(
    string SourceName,
    long Length,
    IReadOnlyList<TelemetryLogIndexEntry> Entries,
    DateTimeOffset? StartedAt,
    DateTimeOffset? EndedAt,
    int AdjustedTimestampCount)
{
    /// <summary>Gets the non-negative recorded duration.</summary>
    public TimeSpan Duration => StartedAt is { } start && EndedAt is { } end ? end - start : TimeSpan.Zero;
}
