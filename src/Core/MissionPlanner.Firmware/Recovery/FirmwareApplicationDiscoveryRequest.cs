using MissionPlanner.Firmware.Model;

namespace MissionPlanner.Firmware.Recovery;

/// <summary>Describes the device transition expected after a bootloader reboot.</summary>
public sealed record FirmwareApplicationDiscoveryRequest(
    SerialDeviceDescriptor BootloaderDevice,
    SerialDeviceDescriptor? OriginalApplicationDevice = null,
    TimeSpan? Timeout = null);
