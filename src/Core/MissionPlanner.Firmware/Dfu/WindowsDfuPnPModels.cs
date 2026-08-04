namespace MissionPlanner.Firmware.Dfu;

/// <summary>Contains raw Windows Plug and Play evidence used by the DFU catalogue.</summary>
public sealed record WindowsDfuPnPSnapshot(
    string InstanceId,
    ushort VendorId,
    ushort ProductId,
    bool IsPresent,
    string? DevicePath = null,
    string? FriendlyName = null,
    string? Manufacturer = null,
    string? UsbSerialNumber = null,
    string? DriverService = null,
    string? DriverProvider = null,
    string? DriverVersion = null,
    int? ProblemCode = null,
    bool IsBusy = false);

/// <summary>Provides raw Windows DFU Plug and Play snapshots behind a fakeable boundary.</summary>
public interface IWindowsDfuPnPSnapshotSource
{
    /// <summary>Gets the current Windows Plug and Play snapshot.</summary>
    Task<IReadOnlyList<WindowsDfuPnPSnapshot>> GetSnapshotsAsync(CancellationToken cancellationToken = default);
}

/// <summary>Waits for a Windows USB device-change notification.</summary>
public interface IWindowsUsbDeviceChangeNotifier
{
    /// <summary>Waits for a device change or returns false when the polling deadline expires.</summary>
    Task<bool> WaitForChangeAsync(TimeSpan pollingDeadline, CancellationToken cancellationToken = default);
}
