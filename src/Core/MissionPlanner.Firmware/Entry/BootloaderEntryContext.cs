using MissionPlanner.Firmware.Discovery;
using MissionPlanner.Firmware.Model;

namespace MissionPlanner.Firmware.Entry;

/// <summary>Contains bootloader-entry inputs independent of UI and MAVLink implementation.</summary>
public sealed record BootloaderEntryContext(
    BootloaderDiscoveryRequest DiscoveryRequest,
    SerialDeviceDescriptor? ApplicationDevice = null,
    bool HasActiveMissionPlannerSession = false);
