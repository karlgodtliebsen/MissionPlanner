namespace MissionPlanner.Firmware.Dfu;

/// <summary>Contains bounded process output and termination evidence.</summary>
public sealed record DfuProcessResult(
    int? ExitCode,
    IReadOnlyList<DfuProcessOutput> Output,
    bool TimedOut = false,
    bool WasCancelled = false,
    string? FailureCode = null,
    bool OutputTruncated = false);
