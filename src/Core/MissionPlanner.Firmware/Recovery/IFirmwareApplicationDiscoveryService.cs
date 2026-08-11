using MissionPlanner.Firmware.Model;

namespace MissionPlanner.Firmware.Recovery;

/// <summary>Finds the returning application device without retaining bootloader transport state.</summary>
public interface IFirmwareApplicationDiscoveryService
{
    /// <summary>Waits for and matches the returning application device, or returns null on a bounded timeout.</summary>
    Task<SerialDeviceDescriptor?> FindAsync(
        FirmwareApplicationDiscoveryRequest request,
        CancellationToken cancellationToken = default);
}
