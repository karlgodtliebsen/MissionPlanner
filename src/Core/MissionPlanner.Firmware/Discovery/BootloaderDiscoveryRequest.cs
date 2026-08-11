using MissionPlanner.Firmware.Model;

namespace MissionPlanner.Firmware.Discovery;

/// <summary>Defines bootloader candidate hints and selection.</summary>
public sealed record BootloaderDiscoveryRequest(
    SerialDeviceDescriptor? SelectedDevice = null,
    IReadOnlyCollection<UsbIdentifier>? ExpectedUsbIdentifiers = null,
    IReadOnlyCollection<string>? BootloaderHints = null,
    TimeSpan? Timeout = null,
    int? BaudRate = null);
