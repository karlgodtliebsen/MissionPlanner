namespace MissionPlanner.Firmware.Connected;

/// <summary>Updates the bootloader image embedded in connected application firmware.</summary>
public interface IEmbeddedBootloaderUpdateService
{
    /// <summary>Checks preconditions and runs only the connected command use case.</summary>
    Task<BootloaderUpdateResult> UpdateAsync(BootloaderUpdateRequest request, CancellationToken cancellationToken = default);
}
