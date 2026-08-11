namespace MissionPlanner.Firmware.Dfu;

/// <summary>Contains one ordered external-tool discovery candidate.</summary>
public sealed record DfuToolCandidate(
    string ExecutablePath,
    DfuToolDiscoverySource Source,
    bool Exists,
    Version? FileVersion = null);
