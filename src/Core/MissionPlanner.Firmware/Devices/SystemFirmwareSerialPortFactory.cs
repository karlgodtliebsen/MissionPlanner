using System.IO.Ports;

namespace MissionPlanner.Firmware.Devices;

/// <summary>Opens a serial port for the firmware workflow's exclusive lifetime.</summary>
public sealed class SystemFirmwareSerialPortFactory : IFirmwareSerialPortFactory
{
    /// <inheritdoc />
    public async Task<IFirmwareSerialPort> OpenAsync(SerialPortOpenOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        var port = new SerialPort(options.PortName, options.BaudRate, Parity.None, 8, StopBits.One)
        {
            ReadTimeout = checked((int)Math.Min(options.ReadTimeout.TotalMilliseconds, int.MaxValue)),
            WriteTimeout = checked((int)Math.Min(options.WriteTimeout.TotalMilliseconds, int.MaxValue))
        };
        try
        {
            await Task.Run(port.Open, cancellationToken).ConfigureAwait(false);
            return new OwnedSerialPort(port);
        }
        catch
        {
            port.Dispose();
            throw;
        }
    }

    private sealed class OwnedSerialPort(SerialPort port) : IFirmwareSerialPort
    {
        public string PortName => port.PortName;
        public Stream Stream => port.BaseStream;
        public bool IsOpen => port.IsOpen;
        public void DiscardInBuffer() => port.DiscardInBuffer();
        public ValueTask DisposeAsync()
        {
            port.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
