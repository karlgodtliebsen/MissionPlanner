using MissionPlanner.Firmware.Discovery;
using MissionPlanner.Firmware.Exceptions;

namespace MissionPlanner.Firmware.Entry;

/// <summary>Directly probes devices that may already be running a bootloader.</summary>
public sealed class AlreadyInBootloaderEntryStrategy(IBootloaderDiscoveryService discovery) : IBootloaderEntryStrategy
{
    /// <inheritdoc />
    public int Priority => 100;

    /// <inheritdoc />
    public async Task<BootloaderEntryResult> TryEnterAsync(BootloaderEntryContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.ApplicationDevice is not null)
        {
            // The selected port was positively identified as the running application device.
            // Probing it with the bootloader protocol starts a native SerialPort read on Windows;
            // if the application does not answer, that timed-out read can retain exclusive COM
            // ownership and prevent the following MAVLink reboot strategy from opening the port.
            return new BootloaderEntryResult(
                BootloaderEntryOutcome.NotApplicable,
                "entry.application-device-not-probed-as-bootloader");
        }

        try
        {
            var request = context.DiscoveryRequest with { Timeout = TimeSpan.FromMilliseconds(250) };
            var found = await discovery.FindAsync(request, cancellationToken: cancellationToken).ConfigureAwait(false);
            return new BootloaderEntryResult(BootloaderEntryOutcome.BootloaderIdentified, "entry.already-in-bootloader", found);
        }
        catch (FirmwareDeviceNotFoundException)
        {
            return new BootloaderEntryResult(BootloaderEntryOutcome.Failed, "entry.bootloader-not-already-present");
        }
    }
}
