namespace MissionPlanner.Firmware.Entry;

/// <summary>Attempts one method of entering or locating a bootloader.</summary>
public interface IBootloaderEntryStrategy
{
    /// <summary>Gets ascending execution priority.</summary>
    int Priority { get; }

    /// <summary>Attempts the strategy without retaining temporary serial ownership.</summary>
    Task<BootloaderEntryResult> TryEnterAsync(BootloaderEntryContext context, CancellationToken cancellationToken = default);
}
