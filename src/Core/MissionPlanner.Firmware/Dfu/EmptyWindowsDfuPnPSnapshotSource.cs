namespace MissionPlanner.Firmware.Dfu;

internal sealed class EmptyWindowsDfuPnPSnapshotSource : IWindowsDfuPnPSnapshotSource
{
    public Task<IReadOnlyList<WindowsDfuPnPSnapshot>> GetSnapshotsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<WindowsDfuPnPSnapshot>>([]);
    }
}
