using Microsoft.Extensions.Options;

namespace MissionPlanner.Firmware.Dfu;

/// <summary>Maps Windows Plug and Play snapshots to typed USB DFU devices without serial enumeration.</summary>
public sealed class WindowsDfuDeviceCatalog(
    IWindowsDfuPnPSnapshotSource snapshots,
    IOptions<DfuOptions> options,
    TimeProvider timeProvider) : IDfuDeviceCatalog
{
    private readonly Dictionary<string, DateTimeOffset> arrivals = new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public async Task<IReadOnlyList<DfuDeviceDescriptor>> GetDevicesAsync(CancellationToken cancellationToken = default)
    {
        var configured = options.Value;
        var now = timeProvider.GetUtcNow();
        var current = await snapshots.GetSnapshotsAsync(cancellationToken).ConfigureAwait(false);
        var presentIds = current.Where(item => item.IsPresent).Select(item => item.InstanceId).ToHashSet(StringComparer.OrdinalIgnoreCase);
        lock (arrivals)
        {
            foreach (var stale in arrivals.Keys.Where(id => !presentIds.Contains(id)).ToArray()) arrivals.Remove(stale);

            return current
                .Where(item => item.IsPresent && item.VendorId == configured.DefaultUsbVendorId && item.ProductId == configured.DefaultUsbProductId)
                .Select(item =>
                {
                    if (!arrivals.TryGetValue(item.InstanceId, out var arrivedAt)) arrivals[item.InstanceId] = arrivedAt = now;
                    return new DfuDeviceDescriptor(
                        item.InstanceId,
                        item.VendorId,
                        item.ProductId,
                        MapDriverState(item, configured.AcceptedWindowsDriverServices),
                        item.FriendlyName,
                        item.Manufacturer,
                        item.UsbSerialNumber,
                        item.DevicePath,
                        item.InstanceId,
                        item.DriverProvider,
                        item.DriverVersion,
                        item.ProblemCode,
                        now,
                        arrivedAt);
                })
                .OrderBy(item => item.ProviderId, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
    }

    private static DfuDriverState MapDriverState(WindowsDfuPnPSnapshot snapshot, IReadOnlyCollection<string> acceptedServices)
    {
        if (!snapshot.IsPresent) return DfuDriverState.NotPresent;
        if (snapshot.IsBusy) return DfuDriverState.Busy;
        if (snapshot.ProblemCode is > 0) return DfuDriverState.PresentWithProblem;
        if (string.IsNullOrWhiteSpace(snapshot.DriverService)) return DfuDriverState.Unknown;
        return acceptedServices.Contains(snapshot.DriverService, StringComparer.OrdinalIgnoreCase)
            ? DfuDriverState.PresentReady
            : DfuDriverState.PresentWrongDriver;
    }
}
