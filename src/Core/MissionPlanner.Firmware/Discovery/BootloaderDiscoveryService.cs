using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
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
                if (found is not null) return found;
            }

            progress?.Report(new FirmwareProgress(FirmwareOperationState.WaitingForDevice, null, "discovery.waiting-for-bootloader"));
            await foreach (var change in monitor.WatchAsync(deadline.Token).ConfigureAwait(false))
            {
                if (change.Kind != FirmwareDeviceChangeKind.Arrived) continue;
                progress?.Report(new FirmwareProgress(FirmwareOperationState.WaitingForDevice, null, "discovery.device-arrived", technicalDetail: change.Device.PortName));
                var found = await ProbeAsync(change.Device, request, probed, deadline.Token).ConfigureAwait(false);
                if (found is not null) return found;
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
        var key = $"{candidate.StableIdentity ?? "transient"}|{candidate.PortName}";
        if (!probed.Add(key)) return null;
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
        catch (Exception exception) when (exception is FirmwareBootloaderException or IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            logger.LogDebug(exception, "Rejected non-bootloader serial candidate {PortName}.", candidate.PortName);
        }
        if (client is not null) await client.DisposeAsync().ConfigureAwait(false);
        else if (port is not null) await port.DisposeAsync().ConfigureAwait(false);
        return null;
    }

    private static IEnumerable<SerialDeviceDescriptor> Rank(IEnumerable<SerialDeviceDescriptor> devices, BootloaderDiscoveryRequest request, bool newlyArrived) =>
        devices.OrderByDescending(device => newlyArrived)
            .ThenByDescending(device => IsSelected(device, request.SelectedDevice))
            .ThenByDescending(device => request.ExpectedUsbIdentifiers?.Contains(device.UsbIdentifier ?? default) == true)
            .ThenByDescending(device => MatchesHint(device, request.BootloaderHints))
            .ThenBy(device => device.PortName, StringComparer.OrdinalIgnoreCase);

    private static bool IsSelected(SerialDeviceDescriptor device, SerialDeviceDescriptor? selected) => selected is not null &&
        ((selected.StableIdentity is not null && string.Equals(device.StableIdentity, selected.StableIdentity, StringComparison.OrdinalIgnoreCase)) ||
         string.Equals(device.PortName, selected.PortName, StringComparison.OrdinalIgnoreCase));

    private static bool MatchesHint(SerialDeviceDescriptor device, IReadOnlyCollection<string>? hints) => hints?.Any(hint =>
        (!string.IsNullOrWhiteSpace(device.ProductName) && device.ProductName.Contains(hint, StringComparison.OrdinalIgnoreCase)) ||
        device.BoardHints.Any(value => value.Contains(hint, StringComparison.OrdinalIgnoreCase))) == true;
}
