namespace MissionPlanner.Firmware.Dfu;

internal sealed class EmptyDfuDeviceCatalog : IDfuDeviceCatalog
{
    public Task<IReadOnlyList<DfuDeviceDescriptor>> GetDevicesAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<DfuDeviceDescriptor>>([]);
    }
}
