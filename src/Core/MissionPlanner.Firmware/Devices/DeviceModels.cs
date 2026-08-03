namespace MissionPlanner.Firmware.Devices;

/// <summary>Identifies the kind of serial device change.</summary>
public enum FirmwareDeviceChangeKind
{
    /// <summary>A device became available.</summary>
    Arrived,
    /// <summary>A device stopped being available.</summary>
    Removed
}

/// <summary>Describes a serial device arrival or removal.</summary>
public sealed record FirmwareDeviceChange(FirmwareDeviceChangeKind Kind, Model.SerialDeviceDescriptor Device, DateTimeOffset Timestamp);

/// <summary>Defines an exclusive serial-port open request.</summary>
public sealed record SerialPortOpenOptions
{
    /// <summary>Initializes serial port settings.</summary>
    public SerialPortOpenOptions(string portName, int baudRate = 115200, TimeSpan? readTimeout = null, TimeSpan? writeTimeout = null)
    {
        PortName = string.IsNullOrWhiteSpace(portName) ? throw new ArgumentException("A port name is required.", nameof(portName)) : portName.Trim();
        if (baudRate <= 0) throw new ArgumentOutOfRangeException(nameof(baudRate));
        BaudRate = baudRate;
        ReadTimeout = readTimeout ?? TimeSpan.FromSeconds(2);
        WriteTimeout = writeTimeout ?? TimeSpan.FromSeconds(2);
        if (ReadTimeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(readTimeout));
        if (WriteTimeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(writeTimeout));
    }

    /// <summary>Gets the transient port name.</summary>
    public string PortName { get; }
    /// <summary>Gets the baud rate.</summary>
    public int BaudRate { get; }
    /// <summary>Gets the bounded read timeout.</summary>
    public TimeSpan ReadTimeout { get; }
    /// <summary>Gets the bounded write timeout.</summary>
    public TimeSpan WriteTimeout { get; }
}
