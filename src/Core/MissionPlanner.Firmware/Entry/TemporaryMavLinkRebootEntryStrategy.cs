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
        cancellationToken.ThrowIfCancellationRequested();
        var device = context.ApplicationDevice ?? context.DiscoveryRequest.SelectedDevice;
        if (context.HasActiveMissionPlannerSession || device is null)
        {
            return new BootloaderEntryResult(BootloaderEntryOutcome.NotApplicable, "entry.temporary-mavlink-not-applicable");
        }

        try
        {
            context.Progress?.Invoke(new(Model.FirmwareOperationState.RequestingBootloaderReboot, null, "entry.requesting-bootloader-reboot"));
            var sent = await gateway.RebootToBootloaderAsync(device, cancellationToken).ConfigureAwait(false);
            // Enumeration, not an ACK or a surviving USB write, determines success.
            return new BootloaderEntryResult(BootloaderEntryOutcome.ContinueDiscovery,
                sent ? "entry.temporary-mavlink-reboot-sent" : "entry.temporary-mavlink-no-heartbeat");
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            return new BootloaderEntryResult(BootloaderEntryOutcome.ContinueDiscovery, "entry.temporary-mavlink-timed-out", TechnicalDetail: exception.Message);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or TimeoutException or FirmwareBootloaderException)
        {
            return new BootloaderEntryResult(BootloaderEntryOutcome.ContinueDiscovery, "entry.temporary-mavlink-failed", TechnicalDetail: exception.Message);
        }
    }
}
