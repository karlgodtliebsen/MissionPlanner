using MissionPlanner.Firmware.Exceptions;

namespace MissionPlanner.Firmware.Entry;

/// <summary>Uses a temporary isolated MAVLink channel when no normal session owns the port.</summary>
public sealed class TemporaryMavLinkRebootEntryStrategy(ITemporaryMavLinkBootloaderGateway gateway) : IBootloaderEntryStrategy
{
    /// <inheritdoc />
    public int Priority => 200;

    /// <inheritdoc />
    public async Task<BootloaderEntryResult> TryEnterAsync(BootloaderEntryContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.HasActiveMissionPlannerSession || context.ApplicationDevice is null)
        {
            return new BootloaderEntryResult(BootloaderEntryOutcome.NotApplicable, "entry.temporary-mavlink-not-applicable");
        }

        try
        {
            var accepted = await gateway.RebootToBootloaderAsync(context.ApplicationDevice, cancellationToken).ConfigureAwait(false);
            return accepted
                ? new BootloaderEntryResult(BootloaderEntryOutcome.ContinueDiscovery, "entry.temporary-mavlink-reboot-sent")
                : new BootloaderEntryResult(BootloaderEntryOutcome.Failed, "entry.temporary-mavlink-reboot-not-accepted");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or TimeoutException or FirmwareBootloaderException)
        {
            return new BootloaderEntryResult(BootloaderEntryOutcome.Failed, "entry.temporary-mavlink-failed", TechnicalDetail: exception.Message);
        }
    }
}
