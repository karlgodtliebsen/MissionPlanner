namespace MissionPlanner.Firmware.Entry;

/// <summary>Runs bootloader-entry strategies in deterministic priority order.</summary>
public interface IBootloaderEntryService
{
    /// <summary>Runs applicable strategies until discovery may proceed or a bootloader is identified.</summary>
    Task<BootloaderEntryResult> EnterAsync(BootloaderEntryContext context, CancellationToken cancellationToken = default);
}
