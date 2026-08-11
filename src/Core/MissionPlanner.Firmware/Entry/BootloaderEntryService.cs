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
        BootloaderEntryResult? last = null;
        foreach (var strategy in strategies.OrderBy(strategy => strategy.Priority))
        {
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
                var found = await discovery.FindAsync(context.DiscoveryRequest, cancellationToken: cancellationToken).ConfigureAwait(false);
                return new BootloaderEntryResult(BootloaderEntryOutcome.BootloaderIdentified, result.Code, found, result.TechnicalDetail);
            }
            catch (FirmwareDeviceNotFoundException exception)
            {
                logger.LogInformation(
                    "Bootloader was not discovered after entry strategy {Strategy}; trying the next strategy.",
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
