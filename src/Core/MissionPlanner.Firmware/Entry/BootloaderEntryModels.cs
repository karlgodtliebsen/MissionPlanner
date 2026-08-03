using MissionPlanner.Firmware.Discovery;
using MissionPlanner.Firmware.Model;

namespace MissionPlanner.Firmware.Entry;

/// <summary>Contains bootloader-entry inputs independent of UI and MAVLink implementation.</summary>
public sealed record BootloaderEntryContext(
    BootloaderDiscoveryRequest DiscoveryRequest,
    SerialDeviceDescriptor? ApplicationDevice = null,
    bool HasActiveMissionPlannerSession = false);

/// <summary>Identifies the result of one bootloader-entry attempt.</summary>
public enum BootloaderEntryOutcome
{
    /// <summary>The strategy does not apply to the current context.</summary>
    NotApplicable,
    /// <summary>The strategy completed and discovery may continue.</summary>
    ContinueDiscovery,
    /// <summary>A bootloader was directly identified.</summary>
    BootloaderIdentified,
    /// <summary>The strategy failed and another strategy may be attempted.</summary>
    Failed
}

/// <summary>Contains a stable bootloader-entry result code and optional identified device.</summary>
public sealed record BootloaderEntryResult(
    BootloaderEntryOutcome Outcome,
    string Code,
    DiscoveredBootloader? Bootloader = null,
    string? TechnicalDetail = null);
