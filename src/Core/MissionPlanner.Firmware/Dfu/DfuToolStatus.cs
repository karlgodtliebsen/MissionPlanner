namespace MissionPlanner.Firmware.Dfu;

/// <summary>Contains validated external-tool discovery evidence.</summary>
public sealed record DfuToolStatus(
    DfuToolAvailability Availability,
    string? ExecutablePath = null,
    Version? Version = null,
    string? Diagnostic = null);
