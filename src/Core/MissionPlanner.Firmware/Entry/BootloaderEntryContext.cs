using MissionPlanner.Firmware.Discovery;
using MissionPlanner.Firmware.Model;

namespace MissionPlanner.Firmware.Entry;

/// <summary>Contains bootloader-entry inputs independent of UI and MAVLink implementation.</summary>
public sealed record BootloaderEntryContext(
    BootloaderDiscoveryRequest DiscoveryRequest,
    SerialDeviceDescriptor? ApplicationDevice = null,
    bool HasActiveMissionPlannerSession = false)
{
    /// <summary>Reports ordered entry stages to the owning firmware operation.</summary>
    public Action<FirmwareProgress>? Progress { get; init; }
}
