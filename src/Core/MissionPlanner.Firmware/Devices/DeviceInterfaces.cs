using MissionPlanner.Firmware.Model;

namespace MissionPlanner.Firmware.Devices;

/// <summary>Provides current serial-device snapshots without opening ports.</summary>
public interface IFirmwareSerialDeviceCatalog
{
    /// <summary>Gets the current deduplicated device snapshot.</summary>
    Task<IReadOnlyList<SerialDeviceDescriptor>> GetDevicesAsync(CancellationToken cancellationToken = default);
}

/// <summary>Monitors serial-device arrivals and removals.</summary>
public interface IFirmwareDeviceMonitor
{
    /// <summary>Watches device changes until cancellation.</summary>
    IAsyncEnumerable<FirmwareDeviceChange> WatchAsync(CancellationToken cancellationToken = default);
}

/// <summary>Opens a serial device for exclusive firmware ownership.</summary>
public interface IFirmwareSerialPortFactory
{
    /// <summary>Opens the requested port or fails if another owner holds it.</summary>
    Task<IFirmwareSerialPort> OpenAsync(SerialPortOpenOptions options, CancellationToken cancellationToken = default);
}

/// <summary>Owns an open serial stream.</summary>
public interface IFirmwareSerialPort : IAsyncDisposable
{
    /// <summary>Gets the transient port name.</summary>
    string PortName { get; }
    /// <summary>Gets the open serial stream.</summary>
    Stream Stream { get; }
    /// <summary>Gets whether the serial port remains open.</summary>
    bool IsOpen { get; }
}
