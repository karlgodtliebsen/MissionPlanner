using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MissionPlanner.Firmware.Configuration;
using MissionPlanner.Firmware.Devices;
using MissionPlanner.Firmware.Exceptions;
using MissionPlanner.Firmware.Model;
using MissionPlanner.Firmware.Protocol;

namespace MissionPlanner.Firmware.Discovery;

/// <summary>Ranks serial candidates and returns only protocol-confirmed bootloaders.</summary>
public sealed class BootloaderDiscoveryService(
    IFirmwareSerialDeviceCatalog catalog,
    IFirmwareDeviceMonitor monitor,
    IFirmwareSerialPortFactory portFactory,
    IArduPilotBootloaderClientFactory clientFactory,
    IOptions<FirmwareOptions> options,
    ILogger<BootloaderDiscoveryService> logger) : IBootloaderDiscoveryService
{
    /// <inheritdoc />
    public async Task<DiscoveredBootloader> FindAsync(
        BootloaderDiscoveryRequest request,
        IProgress<FirmwareProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(request.Timeout ?? options.Value.BootloaderDiscoveryTimeout);
        var probed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var baseline = await catalog.GetDevicesAsync(deadline.Token).ConfigureAwait(false);
            foreach (var candidate in Rank(baseline, request, false))
            {
                var found = await ProbeAsync(candidate, request, probed, deadline.Token).ConfigureAwait(false);
                if (found is not null)
                {
                    return found;
                }
            }

            progress?.Report(new FirmwareProgress(FirmwareOperationState.WaitingForDevice, null, "discovery.waiting-for-bootloader"));
            using var monitorCancellation = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token);
            await using var changes = monitor.WatchAsync(monitorCancellation.Token).GetAsyncEnumerator(monitorCancellation.Token);
            Task<bool>? nextChange = changes.MoveNextAsync().AsTask();
            while (true)
            {
                var poll = Task.Delay(options.Value.BootloaderDiscoveryPollInterval, deadline.Token);
                var completed = nextChange is null
                    ? await Task.WhenAny(poll).ConfigureAwait(false)
                    : await Task.WhenAny(nextChange, poll).ConfigureAwait(false);

                if (nextChange is not null && completed == nextChange)
                {
                    if (!await nextChange.ConfigureAwait(false))
                    {
                        nextChange = null;
                        continue;
                    }

                    var change = changes.Current;
                    nextChange = changes.MoveNextAsync().AsTask();
                    if (change.Kind != FirmwareDeviceChangeKind.Arrived)
                        continue;

                    progress?.Report(new FirmwareProgress(FirmwareOperationState.WaitingForDevice, null, "discovery.device-arrived", technicalDetail: change.Device.PortName));
                    var arrived = await ProbeAsync(change.Device, request, probed, deadline.Token).ConfigureAwait(false);
                    if (arrived is not null)
                    {
                        await StopMonitorAsync().ConfigureAwait(false);
                        return arrived;
                    }
                    continue;
                }

                // Windows often keeps the same COM identity while an ArduPilot controller
                // briefly passes through its bootloader, producing no reliable arrival event.
                // Re-probe current candidates so that short same-port windows are observable.
                probed.Clear();
                var current = await catalog.GetDevicesAsync(deadline.Token).ConfigureAwait(false);
                foreach (var candidate in Rank(current, request, false))
                {
                    var found = await ProbeAsync(candidate, request, probed, deadline.Token).ConfigureAwait(false);
                    if (found is not null)
                    {
                        await StopMonitorAsync().ConfigureAwait(false);
                        return found;
                    }
                }
            }

            async Task StopMonitorAsync()
            {
                monitorCancellation.Cancel();
                if (nextChange is null)
                    return;
                try
                {
                    await nextChange.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // Expected while ending the monitor after a protocol-confirmed probe.
                }
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new FirmwareDeviceNotFoundException("No protocol-compatible bootloader appeared before the discovery deadline.");
        }

        throw new FirmwareDeviceNotFoundException("Bootloader monitoring ended without identifying a device.");
    }

    private async Task<DiscoveredBootloader?> ProbeAsync(
        SerialDeviceDescriptor candidate,
        BootloaderDiscoveryRequest request,
        ISet<string> probed,
        CancellationToken cancellationToken)
    {
        // A controller may leave application mode and return as a bootloader on
        // the same COM port and with the same USB serial number. ArrivedAt
        // distinguishes that new device generation from the rejected baseline.
        var key = $"{candidate.StableIdentity ?? "transient"}|{candidate.PortName}|{candidate.ArrivedAt.UtcTicks}";
        if (!probed.Add(key))
        {
            return null;
        }

        IFirmwareSerialPort? port = null;
        IArduPilotBootloaderClient? client = null;
        try
        {
            using var openDeadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            openDeadline.CancelAfter(options.Value.BootloaderPortOpenTimeout);
            port = await portFactory.OpenAsync(new SerialPortOpenOptions(candidate.PortName, request.BaudRate ?? options.Value.BootloaderBaudRate), openDeadline.Token).ConfigureAwait(false);
            client = clientFactory.Create(port);
            port = null;
            var identity = await client.IdentifyAsync(cancellationToken).ConfigureAwait(false);
            logger.LogInformation("Discovered bootloader board {BoardId} on {PortName} ({OsDeviceId}).", identity.BoardId, candidate.PortName, candidate.OsDeviceId);
            return new DiscoveredBootloader(candidate, identity, client);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogDebug("Timed out opening firmware candidate {PortName}.", candidate.PortName);
        }
        catch (Exception exception) when (exception is FirmwareBootloaderException or TimeoutException or IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            logger.LogDebug(exception, "Rejected non-bootloader serial candidate {PortName}.", candidate.PortName);
        }

        if (client is not null)
        {
            await client.DisposeAsync().ConfigureAwait(false);
        }
        else if (port is not null)
        {
            await port.DisposeAsync().ConfigureAwait(false);
        }

        return null;
    }

    private static IEnumerable<SerialDeviceDescriptor> Rank(IEnumerable<SerialDeviceDescriptor> devices, BootloaderDiscoveryRequest request, bool newlyArrived)
    {
        return devices.OrderByDescending(device => newlyArrived)
            .ThenByDescending(device => IsSelected(device, request.SelectedDevice))
            .ThenByDescending(device => request.ExpectedUsbIdentifiers?.Contains(device.UsbIdentifier ?? default) == true)
            .ThenByDescending(device => MatchesHint(device, request.BootloaderHints))
            .ThenBy(device => device.PortName, StringComparer.OrdinalIgnoreCase);
    }

    private static bool IsSelected(SerialDeviceDescriptor device, SerialDeviceDescriptor? selected)
    {
        return selected is not null &&
               ((selected.StableIdentity is not null && string.Equals(device.StableIdentity, selected.StableIdentity, StringComparison.OrdinalIgnoreCase)) ||
                string.Equals(device.PortName, selected.PortName, StringComparison.OrdinalIgnoreCase));
    }

    private static bool MatchesHint(SerialDeviceDescriptor device, IReadOnlyCollection<string>? hints)
    {
        return hints?.Any(hint =>
            (!string.IsNullOrWhiteSpace(device.ProductName) && device.ProductName.Contains(hint, StringComparison.OrdinalIgnoreCase)) ||
            device.BoardHints.Any(value => value.Contains(hint, StringComparison.OrdinalIgnoreCase))) == true;
    }
}
