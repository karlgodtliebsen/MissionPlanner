namespace MissionPlanner.Core.Setup.Advanced.Inspector;

/// <summary>Identifies one aggregate without exposing MAVLink wire objects to presentation.</summary>
public sealed record InspectorKey(string Direction, byte SystemId, byte ComponentId, uint MessageId);

/// <summary>Bounded aggregate statistics; rates use five one-second buckets including the current second.</summary>
public sealed record InspectorRow(InspectorKey Key, string Name, long Count, long Bytes,
    DateTimeOffset FirstSeen, DateTimeOffset LastSeen, double MessagesPerSecond, double BytesPerSecond,
    int PayloadLength, byte Sequence, string Verification);

/// <summary>Selected message details; raw bytes include the full original signature.</summary>
public sealed record InspectorDetails(InspectorRow Row, string RawHex, IReadOnlyDictionary<string, string> Fields);

/// <summary>Immutable, bounded export suitable for file services.</summary>
public sealed record InspectorSnapshot(DateTimeOffset CapturedAt, long Dropped, IReadOnlyList<InspectorDetails> Messages);
