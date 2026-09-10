namespace MissionPlanner.Firmware.Workflow;

/// <summary>Protocol-observed runtime and boot state for one physical presence generation.</summary>
/// <param name="Runtime">Application runtime proven by protocol.</param>
/// <param name="BootEnvironment">Observed boot environment.</param>
/// <param name="Code">Stable diagnostic outcome.</param>
/// <param name="IsArmed">Live armed evidence when available.</param>
public sealed record FirmwareRuntimeProbeResult(FirmwareRuntimeKind Runtime, FirmwareBootEnvironment BootEnvironment,
    string Code, bool? IsArmed = null);
