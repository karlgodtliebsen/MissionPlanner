namespace MissionPlanner.Core.Replay;

/// <summary>Contains one indexed telemetry-log packet.</summary>
/// <param name="Entry">Packet index metadata.</param>
/// <param name="Packet">Complete MAVLink frame bytes without the timestamp prefix.</param>
public sealed record TelemetryLogRecord(TelemetryLogIndexEntry Entry, ReadOnlyMemory<byte> Packet);
