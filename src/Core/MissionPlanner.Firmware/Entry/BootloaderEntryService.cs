using Microsoft.Extensions.Logging;
using MissionPlanner.Firmware.Discovery;
using MissionPlanner.Firmware.Exceptions;

namespace MissionPlanner.Firmware.Entry;

/// <summary>Runs entry strategies, allowing recoverable failures to fall through.</summary>
public sealed class BootloaderEntryService(
    IEnumerable<IBootloaderEntryStrategy> strategies,
    IBootloaderDiscoveryService discovery,
    ILogger<BootloaderEntryService> logger) : IBootloaderEntryService
{
    /// <inheritdoc />
    public async Task<BootloaderEntryResult> EnterAsync(BootloaderEntryContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        if (context.HasActiveMissionPlannerSession)
        {
            throw new FirmwareConnectionConflictException("The vehicle session must release the serial port before bootloader entry.");
        }
        context = context with { DiscoveryRequest = context.DiscoveryRequest with
        {
            SelectedDevice = context.DiscoveryRequest.SelectedDevice ?? context.ApplicationDevice
        } };
        logger.LogInformation("Selected firmware device {DeviceIdentity}, application endpoint {PortName}. Checking for an existing ArduPilot bootloader.",
            context.DiscoveryRequest.SelectedDevice?.StableIdentity, context.DiscoveryRequest.SelectedDevice?.PortName);
        BootloaderEntryResult? last = null;
        foreach (var strategy in strategies.OrderBy(strategy => strategy.Priority))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await strategy.TryEnterAsync(context, cancellationToken).ConfigureAwait(false);
            logger.LogInformation("Bootloader entry strategy {Strategy} returned {Outcome} ({Code}).", strategy.GetType().Name, result.Outcome, result.Code);
            last = result;
            if (result.Outcome is BootloaderEntryOutcome.BootloaderIdentified)
            {
                return result;
            }

            if (result.Outcome is not BootloaderEntryOutcome.ContinueDiscovery)
            {
                continue;
            }

            try
            {
                context.Progress?.Invoke(new(Model.FirmwareOperationState.WaitingForBootloader, null, "entry.waiting-for-bootloader"));
                logger.LogInformation("Waiting for ArduPilot bootloader enumeration after {Strategy}.", strategy.GetType().Name);
                var found = await discovery.FindAsync(context.DiscoveryRequest, cancellationToken: cancellationToken).ConfigureAwait(false);
                return new BootloaderEntryResult(BootloaderEntryOutcome.BootloaderIdentified, result.Code, found, result.TechnicalDetail);
            }
            catch (FirmwareDeviceNotFoundException exception)
            {
                logger.LogInformation(
                    "ArduPilot bootloader detection timed out after {Strategy}; continuing to the manual reset/reconnect fallback if available.",
                    strategy.GetType().Name);
                last = new BootloaderEntryResult(
                    BootloaderEntryOutcome.Failed,
                    "entry.discovery-after-strategy-failed",
                    TechnicalDetail: exception.Message);
            }
        }

        return last ?? new BootloaderEntryResult(BootloaderEntryOutcome.Failed, "entry.no-strategies-registered");
    }
}
