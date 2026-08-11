using MissionPlanner.Firmware.Discovery;

namespace MissionPlanner.Firmware.Entry;

/// <summary>Contains a stable bootloader-entry result code and optional identified device.</summary>
public sealed record BootloaderEntryResult(
    BootloaderEntryOutcome Outcome,
    string Code,
    DiscoveredBootloader? Bootloader = null,
    string? TechnicalDetail = null);
