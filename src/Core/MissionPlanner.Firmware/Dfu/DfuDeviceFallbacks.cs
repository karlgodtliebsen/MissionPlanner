namespace MissionPlanner.Firmware.Dfu;

internal sealed class EmptyWindowsDfuPnPSnapshotSource : IWindowsDfuPnPSnapshotSource
{
    public Task<IReadOnlyList<WindowsDfuPnPSnapshot>> GetSnapshotsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<WindowsDfuPnPSnapshot>>([]);
    }
}

internal sealed class EmptyDfuToolDiscoverySource : IDfuToolDiscoverySource
{
    public IReadOnlyList<DfuToolCandidate> Discover() => [];
}

internal sealed class UnsupportedDfuToolLocator : IDfuToolLocator
{
    public Task<DfuToolStatus> LocateAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new DfuToolStatus(DfuToolAvailability.NotInstalled, Diagnostic: "STM32CubeProgrammer discovery is supported on Windows only."));
    }
}

internal sealed class UnavailableDfuProcessRunner : IDfuProcessRunner
{
    public Task<DfuProcessResult> RunAsync(DfuProcessRequest request, IProgress<DfuProcessOutput>? output = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new DfuProcessResult(null, [], FailureCode: "ProcessRunnerUnavailable"));
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
