namespace MissionPlanner.Firmware.Dfu;

/// <summary>Contains one timestamped external-provider output line.</summary>
public sealed record DfuProcessOutput(DateTimeOffset Timestamp, bool IsError, string Text);
