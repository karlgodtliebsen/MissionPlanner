namespace MissionPlanner.Firmware.Devices;

/// <summary>Monitors serial-device arrivals and removals.</summary>
public interface IFirmwareDeviceMonitor
{
    /// <summary>Watches device changes until cancellation.</summary>
    IAsyncEnumerable<FirmwareDeviceChange> WatchAsync(CancellationToken cancellationToken = default);
}
