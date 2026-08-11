namespace MissionPlanner.Firmware.Dfu;

internal sealed class PollingDfuDeviceChangeNotifier : IWindowsUsbDeviceChangeNotifier
{
    public async Task<bool> WaitForChangeAsync(TimeSpan pollingDeadline, CancellationToken cancellationToken = default)
    {
        await Task.Delay(pollingDeadline, cancellationToken).ConfigureAwait(false);
        return false;
    }
}
