namespace MissionPlanner.Firmware.Dfu;

/// <summary>Waits for a Windows USB device-change notification.</summary>
public interface IWindowsUsbDeviceChangeNotifier
{
    /// <summary>Waits for a device change or returns false when the polling deadline expires.</summary>
    Task<bool> WaitForChangeAsync(TimeSpan pollingDeadline, CancellationToken cancellationToken = default);
}
