using MissionPlanner.Firmware.Model;

namespace MissionPlanner.Firmware.Entry;

/// <summary>Creates and disposes an isolated temporary MAVLink channel for one reboot request.</summary>
public interface ITemporaryMavLinkBootloaderGateway
{
    /// <summary>Detects a heartbeat, sends a bootloader reboot request, and immediately disposes the channel so discovery can own the port.</summary>
    Task<bool> RebootToBootloaderAsync(SerialDeviceDescriptor applicationDevice, CancellationToken cancellationToken = default);
}
