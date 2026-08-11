namespace MissionPlanner.Firmware.Dfu;

/// <summary>Provides raw Windows DFU Plug and Play snapshots behind a fakeable boundary.</summary>
public interface IWindowsDfuPnPSnapshotSource
{
    /// <summary>Gets the current Windows Plug and Play snapshot.</summary>
    Task<IReadOnlyList<WindowsDfuPnPSnapshot>> GetSnapshotsAsync(CancellationToken cancellationToken = default);
}
