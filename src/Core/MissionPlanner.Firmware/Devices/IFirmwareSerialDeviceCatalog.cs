using MissionPlanner.Firmware.Model;

namespace MissionPlanner.Firmware.Devices;

/// <summary>Provides current serial-device snapshots without opening ports.</summary>
public interface IFirmwareSerialDeviceCatalog
{
    /// <summary>Gets the current deduplicated device snapshot.</summary>
    Task<IReadOnlyList<SerialDeviceDescriptor>> GetDevicesAsync(CancellationToken cancellationToken = default);
}
