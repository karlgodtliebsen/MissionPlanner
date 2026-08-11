using MissionPlanner.Firmware.Model;

namespace MissionPlanner.Firmware.Entry;

/// <summary>Requests a user-assisted reconnect or reset through a host interaction boundary.</summary>
public sealed class ManualReconnectBootloaderEntryStrategy(IBootloaderEntryInteraction interaction) : IBootloaderEntryStrategy
{
    /// <inheritdoc />
    public int Priority => 300;

    /// <inheritdoc />
    public async Task<BootloaderEntryResult> TryEnterAsync(BootloaderEntryContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        var accepted = await interaction.RequestAsync(FirmwareInteractionCodes.ManualBootloaderReconnect, cancellationToken).ConfigureAwait(false);
        if (!accepted)
        {
            throw new OperationCanceledException("The operator rejected the manual bootloader reconnect request.");
        }

        return new BootloaderEntryResult(BootloaderEntryOutcome.ContinueDiscovery, "entry.manual-reconnect-requested");
    }
}
