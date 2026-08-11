namespace MissionPlanner.Firmware.Dfu;

/// <summary>Contains provider-reported MCU evidence that does not prove the flight-controller PCB.</summary>
public sealed record DfuDeviceInformation(
    DfuDeviceDescriptor Device,
    string? McuDeviceId,
    string? Revision,
    long? InternalFlashBytes,
    IReadOnlyList<DfuMemoryRange> WritableRanges,
    IReadOnlyList<string> Warnings,
    string? ProviderLog = null);
