namespace MissionPlanner.Firmware.Devices;

/// <summary>Opens a serial device for exclusive firmware ownership.</summary>
public interface IFirmwareSerialPortFactory
{
    /// <summary>Opens the requested port or fails if another owner holds it.</summary>
    Task<IFirmwareSerialPort> OpenAsync(SerialPortOpenOptions options, CancellationToken cancellationToken = default);
}
