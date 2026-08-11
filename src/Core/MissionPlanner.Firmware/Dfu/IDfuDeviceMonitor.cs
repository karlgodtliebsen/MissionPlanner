namespace MissionPlanner.Firmware.Dfu;

/// <summary>Observes USB DFU device arrival, removal, and driver-state changes.</summary>
public interface IDfuDeviceMonitor
{
    /// <summary>Watches changing typed USB DFU device snapshots.</summary>
    IAsyncEnumerable<IReadOnlyList<DfuDeviceDescriptor>> WatchAsync(CancellationToken cancellationToken = default);
}
