using MissionPlanner.Core.Vehicles.Models;

namespace MissionPlanner.Core.Replay;

/// <summary>Identifies the lifecycle state of an isolated telemetry-log replay.</summary>
public enum ReplaySessionState
{
    /// <summary>No telemetry log is loaded and outbound transmission is allowed.</summary>
    Unloaded,

    /// <summary>The telemetry log is being structurally indexed.</summary>
    Indexing,

    /// <summary>The indexed log is ready at its current position.</summary>
    Ready,

    /// <summary>Frames are advancing according to the replay clock.</summary>
    Playing,

    /// <summary>Playback is paused at an indexed position.</summary>
    Paused,

    /// <summary>Every indexed frame has been replayed.</summary>
    Completed,

    /// <summary>Indexing or playback failed and the log remains read-only.</summary>
    Failed
}

/// <summary>Locates one timestamped MAVLink packet inside a Mission Planner telemetry log.</summary>
/// <param name="FrameNumber">Zero-based packet number.</param>
/// <param name="TimestampOffset">Byte offset of the big-endian timestamp.</param>
/// <param name="PacketOffset">Byte offset of the MAVLink packet.</param>
/// <param name="PacketLength">Complete MAVLink packet length.</param>
/// <param name="Timestamp">Normalized UTC packet timestamp.</param>
public sealed record TelemetryLogIndexEntry(
    int FrameNumber,
    long TimestampOffset,
    long PacketOffset,
    int PacketLength,
    DateTimeOffset Timestamp);

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

/// <summary>Contains one indexed telemetry-log packet.</summary>
/// <param name="Entry">Packet index metadata.</param>
/// <param name="Packet">Complete MAVLink frame bytes without the timestamp prefix.</param>
public sealed record TelemetryLogRecord(TelemetryLogIndexEntry Entry, ReadOnlyMemory<byte> Packet);

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

/// <summary>Contains the immutable state of one isolated telemetry-log replay.</summary>
/// <param name="SessionId">Replay-session identity.</param>
/// <param name="State">Replay lifecycle state.</param>
/// <param name="Index">Loaded telemetry-log index.</param>
/// <param name="NextFrameIndex">Index of the next frame to process.</param>
/// <param name="Clock">Current replay clock.</param>
/// <param name="Vehicles">Read-only vehicle states projected only from replay frames.</param>
/// <param name="DecodedFrames">Number of successfully decoded frames since the latest load or seek.</param>
/// <param name="RejectedFrames">Number of structurally indexed frames rejected by parser or decoder.</param>
/// <param name="Message">User-facing status.</param>
/// <param name="Failure">Failure detail when applicable.</param>
public sealed record ReplaySessionSnapshot(
    Guid SessionId,
    ReplaySessionState State,
    TelemetryLogIndex? Index,
    int NextFrameIndex,
    ReplayClockSnapshot? Clock,
    IReadOnlyList<VehicleState> Vehicles,
    long DecodedFrames,
    long RejectedFrames,
    string Message,
    string? Failure)
{
    /// <summary>Gets whether a loaded replay currently prohibits every outbound MAVLink send.</summary>
    public bool IsTransmissionProhibited => State != ReplaySessionState.Unloaded;

    /// <summary>Gets fractional playback progress from zero through one.</summary>
    public double Progress => Index is not { Entries.Count: > 0 } index
        ? 0
        : Math.Clamp(NextFrameIndex / (double)index.Entries.Count, 0, 1);

    /// <summary>Gets the initial unloaded replay state.</summary>
    public static ReplaySessionSnapshot Unloaded { get; } = new(
        Guid.Empty,
        ReplaySessionState.Unloaded,
        null,
        0,
        null,
        [],
        0,
        0,
        "No telemetry log is loaded. Live and simulation transmission is enabled.",
        null);
}

/// <summary>Provides replay state-change event data.</summary>
/// <param name="snapshot">The new immutable replay state.</param>
public sealed class ReplaySessionChangedEventArgs(ReplaySessionSnapshot snapshot) : EventArgs
{
    /// <summary>Gets the new replay state.</summary>
    public ReplaySessionSnapshot Snapshot { get; } = snapshot;
}
