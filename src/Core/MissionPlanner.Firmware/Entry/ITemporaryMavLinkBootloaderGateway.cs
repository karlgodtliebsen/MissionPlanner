using MissionPlanner.Firmware.Model;

namespace MissionPlanner.Firmware.Entry;

/// <summary>Creates and disposes an isolated temporary MAVLink channel for one reboot request.</summary>
public interface ITemporaryMavLinkBootloaderGateway
{
    /// <summary>Detects a heartbeat, requests bootloader reboot, observes acknowledgement when available, and disposes the channel.</summary>
    Task<bool> RebootToBootloaderAsync(SerialDeviceDescriptor applicationDevice, CancellationToken cancellationToken = default);
}
