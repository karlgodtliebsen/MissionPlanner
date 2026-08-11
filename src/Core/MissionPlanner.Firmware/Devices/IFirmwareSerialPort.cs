namespace MissionPlanner.Firmware.Devices;

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
