using MissionPlanner.Firmware.Discovery;
using MissionPlanner.Firmware.Exceptions;
using Microsoft.Extensions.Options;
using MissionPlanner.Firmware.Configuration;

namespace MissionPlanner.Firmware.Entry;

/// <summary>Directly probes devices that may already be running a bootloader.</summary>
public sealed class AlreadyInBootloaderEntryStrategy(IBootloaderDiscoveryService discovery, IOptions<FirmwareOptions>? options = null) : IBootloaderEntryStrategy
{
    /// <inheritdoc />
    public int Priority => 100;

    /// <inheritdoc />
    public async Task<BootloaderEntryResult> TryEnterAsync(BootloaderEntryContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        if (context.HasActiveMissionPlannerSession)
        {
            return new BootloaderEntryResult(
                BootloaderEntryOutcome.NotApplicable,
                "entry.port-owned-by-vehicle-session");
        }

        context.Progress?.Invoke(new(Model.FirmwareOperationState.CheckingForBootloader, null, "entry.checking-for-bootloader"));
        try
        {
            var request = context.DiscoveryRequest with { Timeout = (options?.Value ?? new FirmwareOptions()).BootloaderInitialProbeTimeout };
            var found = await discovery.FindAsync(request, cancellationToken: cancellationToken).ConfigureAwait(false);
            return new BootloaderEntryResult(BootloaderEntryOutcome.BootloaderIdentified, "entry.already-in-bootloader", found);
        }
        catch (FirmwareDeviceNotFoundException)
        {
            return new BootloaderEntryResult(BootloaderEntryOutcome.Failed, "entry.bootloader-not-already-present");
        }
    }
}
