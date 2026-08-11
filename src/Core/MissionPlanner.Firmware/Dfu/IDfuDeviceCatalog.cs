namespace MissionPlanner.Firmware.Dfu;

/// <summary>Provides point-in-time USB DFU device snapshots.</summary>
public interface IDfuDeviceCatalog
{
    /// <summary>Gets the current typed USB DFU device snapshot.</summary>
    Task<IReadOnlyList<DfuDeviceDescriptor>> GetDevicesAsync(CancellationToken cancellationToken = default);
}
