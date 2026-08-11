namespace MissionPlanner.Core.Replay;

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
