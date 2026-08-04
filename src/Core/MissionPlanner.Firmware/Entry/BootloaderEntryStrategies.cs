using Microsoft.Extensions.Logging;
using MissionPlanner.Firmware.Discovery;
using MissionPlanner.Firmware.Exceptions;
using MissionPlanner.Firmware.Model;

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
            return new BootloaderEntryResult(BootloaderEntryOutcome.NotApplicable, "entry.temporary-mavlink-not-applicable");
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
                return result;

            if (result.Outcome is not BootloaderEntryOutcome.ContinueDiscovery)
                continue;

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
