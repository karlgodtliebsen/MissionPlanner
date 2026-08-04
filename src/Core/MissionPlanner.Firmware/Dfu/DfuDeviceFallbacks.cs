namespace MissionPlanner.Firmware.Dfu;

internal sealed class EmptyWindowsDfuPnPSnapshotSource : IWindowsDfuPnPSnapshotSource
{
    public Task<IReadOnlyList<WindowsDfuPnPSnapshot>> GetSnapshotsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<WindowsDfuPnPSnapshot>>([]);
    }
}

internal sealed class EmptyDfuDeviceCatalog : IDfuDeviceCatalog
{
    public Task<IReadOnlyList<DfuDeviceDescriptor>> GetDevicesAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<DfuDeviceDescriptor>>([]);
    }
}

internal sealed class PollingDfuDeviceChangeNotifier : IWindowsUsbDeviceChangeNotifier
{
    public async Task<bool> WaitForChangeAsync(TimeSpan pollingDeadline, CancellationToken cancellationToken = default)
    {
        await Task.Delay(pollingDeadline, cancellationToken).ConfigureAwait(false);
        return false;
    }
}
