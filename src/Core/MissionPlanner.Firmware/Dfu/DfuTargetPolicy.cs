namespace MissionPlanner.Firmware.Dfu;

/// <summary>Defines known MCU and flash constraints for one exact ArduPilot target.</summary>
public sealed record DfuTargetPolicy(
    string Platform,
    int? BoardId,
    IReadOnlyList<string> CompatibleMcuDeviceIds,
    long? MinimumInternalFlashBytes = null,
    long? MaximumInternalFlashBytes = null);
