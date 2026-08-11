namespace MissionPlanner.Firmware.Dfu;

/// <summary>Describes a controlled direct process invocation.</summary>
public sealed record DfuProcessRequest(
    string ExecutablePath,
    IReadOnlyList<string> Arguments,
    TimeSpan StartupTimeout,
    TimeSpan ExecutionTimeout,
    bool MayKillProcessTreeOnCancellation = false,
    DfuProcessPurpose Purpose = DfuProcessPurpose.ValidateTool);
