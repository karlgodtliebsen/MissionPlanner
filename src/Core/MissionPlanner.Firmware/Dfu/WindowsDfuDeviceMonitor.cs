using Microsoft.Extensions.Options;

namespace MissionPlanner.Firmware.Dfu;

/// <summary>Observes Windows USB notifications and retains polling as a bounded fallback.</summary>
public sealed class WindowsDfuDeviceMonitor(
    IDfuDeviceCatalog catalog,
    IWindowsUsbDeviceChangeNotifier notifier,
    IOptions<DfuOptions> options) : IDfuDeviceMonitor
{
    /// <inheritdoc />
    public async IAsyncEnumerable<IReadOnlyList<DfuDeviceDescriptor>> WatchAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        string? previousFingerprint = null;
        while (!cancellationToken.IsCancellationRequested)
        {
            var snapshot = await catalog.GetDevicesAsync(cancellationToken).ConfigureAwait(false);
            var fingerprint = string.Join('|', snapshot.Select(DeviceFingerprint));
            if (!string.Equals(previousFingerprint, fingerprint, StringComparison.Ordinal))
            {
                previousFingerprint = fingerprint;
                yield return snapshot;
            }

            await notifier.WaitForChangeAsync(options.Value.DevicePollInterval, cancellationToken).ConfigureAwait(false);
        }
    }

    private static string DeviceFingerprint(DfuDeviceDescriptor device) =>
        $"{device.ProviderId}\0{device.DriverState}\0{device.DriverVersion}\0{device.ProblemCode}";
}
