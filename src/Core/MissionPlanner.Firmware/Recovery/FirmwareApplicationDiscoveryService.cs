using Microsoft.Extensions.Options;
using MissionPlanner.Firmware.Devices;
using MissionPlanner.Firmware.Model;

namespace MissionPlanner.Firmware.Recovery;

/// <summary>Matches bootloader-to-application USB transitions independently of transient port names.</summary>
public sealed class FirmwareApplicationDiscoveryService(
    IFirmwareSerialDeviceCatalog catalog,
    IFirmwareDeviceMonitor monitor,
    IOptions<FirmwareOptions> options) : IFirmwareApplicationDiscoveryService
{
    /// <inheritdoc />
    public async Task<SerialDeviceDescriptor?> FindAsync(
        FirmwareApplicationDiscoveryRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var timeout = request.Timeout ?? options.Value.BootloaderDiscoveryTimeout;
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);
        var token = timeoutSource.Token;
        try
        {
            var current = await catalog.GetDevicesAsync(token).ConfigureAwait(false);
            var removalObserved = current.All(device => !SameDevice(device, request.BootloaderDevice));
            var existing = BestMatch(current, request, requirePositiveIdentity: true);
            if (removalObserved && existing is not null) return existing;

            await foreach (var change in monitor.WatchAsync(token).ConfigureAwait(false))
            {
                if (change.Kind == FirmwareDeviceChangeKind.Removed && SameDevice(change.Device, request.BootloaderDevice))
                {
                    removalObserved = true;
                    continue;
                }

                if (change.Kind == FirmwareDeviceChangeKind.Arrived && removalObserved && Score(change.Device, request) > 0)
                    return change.Device;
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { }

        cancellationToken.ThrowIfCancellationRequested();
        return null;
    }

    private static SerialDeviceDescriptor? BestMatch(
        IEnumerable<SerialDeviceDescriptor> devices,
        FirmwareApplicationDiscoveryRequest request,
        bool requirePositiveIdentity) => devices
        .Select(device => (Device: device, Score: Score(device, request)))
        .Where(candidate => !requirePositiveIdentity || candidate.Score > 0)
        .OrderByDescending(candidate => candidate.Score)
        .ThenByDescending(candidate => candidate.Device.ArrivedAt)
        .Select(candidate => candidate.Device)
        .FirstOrDefault();

    private static int Score(SerialDeviceDescriptor candidate, FirmwareApplicationDiscoveryRequest request)
    {
        if (SameDevice(candidate, request.BootloaderDevice)) return 0;
        var original = request.OriginalApplicationDevice;
        var score = 0;
        if (original?.UsbSerialNumber is not null && candidate.UsbSerialNumber == original.UsbSerialNumber) score += 100;
        if (original?.OsDeviceId is not null && candidate.OsDeviceId == original.OsDeviceId) score += 80;
        if (request.BootloaderDevice.UsbSerialNumber is not null && candidate.UsbSerialNumber == request.BootloaderDevice.UsbSerialNumber) score += 70;
        if (original?.UsbIdentifier is not null && candidate.UsbIdentifier == original.UsbIdentifier) score += 40;
        if (request.BootloaderDevice.UsbIdentifier is not null && candidate.UsbIdentifier == request.BootloaderDevice.UsbIdentifier) score += 25;
        if (candidate.ArrivedAt >= request.BootloaderDevice.ArrivedAt) score += 5;
        if (candidate.ProductName is not null && !candidate.ProductName.Contains("bootloader", StringComparison.OrdinalIgnoreCase)) score += 5;
        return score;
    }

    private static bool SameDevice(SerialDeviceDescriptor left, SerialDeviceDescriptor right) =>
        left.StableIdentity is not null && left.StableIdentity == right.StableIdentity ||
        left.PortName.Equals(right.PortName, StringComparison.OrdinalIgnoreCase);
}
