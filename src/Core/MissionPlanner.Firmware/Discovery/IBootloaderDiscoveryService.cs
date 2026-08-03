using MissionPlanner.Firmware.Model;

namespace MissionPlanner.Firmware.Discovery;

/// <summary>Finds and protocol-identifies an ArduPilot bootloader.</summary>
public interface IBootloaderDiscoveryService
{
    /// <summary>Waits for and identifies the most likely bootloader candidate.</summary>
    Task<DiscoveredBootloader> FindAsync(
        BootloaderDiscoveryRequest request,
        IProgress<FirmwareProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
