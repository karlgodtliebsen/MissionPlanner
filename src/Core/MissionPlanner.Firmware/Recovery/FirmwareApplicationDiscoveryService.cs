using Microsoft.Extensions.Options;
using MissionPlanner.Firmware.Configuration;
using MissionPlanner.Firmware.Devices;
using MissionPlanner.Firmware.Model;

namespace MissionPlanner.Firmware.Recovery;

/// <summary>Finds the same physical controller after reboot, including same-port transitions without USB events.</summary>
public sealed class FirmwareApplicationDiscoveryService(
    IFirmwareSerialDeviceCatalog catalog,
    IFirmwareDeviceMonitor monitor,
    IOptions<FirmwareOptions> options) : IFirmwareApplicationDiscoveryService
{
    /// <inheritdoc />
    public async Task<SerialDeviceDescriptor?> FindAsync(FirmwareApplicationDiscoveryRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(request.Timeout ?? options.Value.BootloaderDiscoveryTimeout);
        using var watch = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token);
        var changes = monitor.WatchAsync(watch.Token).GetAsyncEnumerator(watch.Token);
        Task<bool>? next = null;
        try
        {
            next = changes.MoveNextAsync().AsTask();
            while (true)
            {
                var matches = (await catalog.GetDevicesAsync(deadline.Token).ConfigureAwait(false))
                    .Where(device => Matches(device, request)).ToArray();
                if (matches.Length == 1)
                {
                    // This is only endpoint discovery; the caller must prove application identity by MAVLink.
                    return matches[0];
                }
                var poll = Task.Delay(options.Value.BootloaderDiscoveryPollInterval, deadline.Token);
                if (next is not null && await Task.WhenAny(next, poll).ConfigureAwait(false) == next)
                {
                    if (!await next.ConfigureAwait(false))
                    {
                        next = null;
                        continue;
                    }
                    var change = changes.Current;
                    next = changes.MoveNextAsync().AsTask();
                    if (change.Kind == FirmwareDeviceChangeKind.Arrived && Matches(change.Device, request))
                    {
                        return change.Device;
                    }
                }
                else
                {
                    await poll.ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
        finally
        {
            watch.Cancel();
            if (next is not null)
            {
                try
                {
                    await next.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                }
            }
            await changes.DisposeAsync().ConfigureAwait(false);
        }
    }

    private static bool Matches(SerialDeviceDescriptor candidate, FirmwareApplicationDiscoveryRequest request)
    {
        var original = request.OriginalApplicationDevice ?? request.BootloaderDevice;
        var serial = original.UsbSerialNumber ?? request.BootloaderDevice.UsbSerialNumber;
        if (serial is not null)
        {
            return string.Equals(candidate.UsbSerialNumber, serial, StringComparison.OrdinalIgnoreCase);
        }
        if (original.OsDeviceId is not null && candidate.OsDeviceId is not null)
        {
            return string.Equals(candidate.OsDeviceId, original.OsDeviceId, StringComparison.OrdinalIgnoreCase);
        }
        // Without stable identity, only the explicitly selected endpoint is eligible.
        return string.Equals(candidate.PortName, original.PortName, StringComparison.OrdinalIgnoreCase);
    }
}
