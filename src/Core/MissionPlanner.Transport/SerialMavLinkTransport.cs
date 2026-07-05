using System.IO.Ports;
using System.Net;
using Microsoft.Extensions.Logging;

namespace MissionPlanner.Transport;

/// <summary>
/// Represents a MAVLink transport that communicates over a serial port.
/// </summary>
public sealed class SerialMavLinkTransport : IMavLinkTransport
{
    private readonly ILogger<SerialMavLinkTransport> logger;
    private readonly SerialPort serialPort;
    private readonly MavLinkEndpoint endpoint;

    /// <summary>
    /// Initializes a new instance of the <see cref="SerialMavLinkTransport"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="portName">The name of the serial port.</param>
    /// <param name="baudRate">The baud rate for the serial port.</param>
    /// <exception cref="ArgumentException">Thrown when the port name is null or whitespace.</exception>
    public SerialMavLinkTransport(ILogger<SerialMavLinkTransport> logger, string portName, int baudRate = 115200)
    {
        if (string.IsNullOrWhiteSpace(portName))
        {
            throw new ArgumentException("LocalPort name must be specified.", nameof(portName));
        }

        this.logger = logger;

        serialPort = new SerialPort(portName, baudRate, Parity.None, 8, StopBits.One) { ReadTimeout = Timeout.Infinite, WriteTimeout = Timeout.Infinite };

        endpoint = new MavLinkEndpoint("serial", portName);
    }

    /// <inheritdoc />
    public bool IsConnected => serialPort.IsOpen;

    /// <inheritdoc />
    public Task ConnectAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!serialPort.IsOpen)
        {
            serialPort.Open();
        }

        logger.LogTrace("Serial port {PortName} opened at {BaudRate} baud.", serialPort.PortName, serialPort.BaudRate);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async ValueTask<TransportReceiveResult> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        if (!IsConnected)
        {
            throw new InvalidOperationException("Serial port is closed.");
        }

        var bytesRead = await serialPort.BaseStream
            .ReadAsync(buffer, cancellationToken)
            .ConfigureAwait(false);
        logger.LogTrace("Read {BytesRead} bytes from serial port {PortName}.", bytesRead, serialPort.PortName);
        return new TransportReceiveResult(bytesRead, endpoint);
    }

    /// <inheritdoc />
    public async ValueTask WriteAsync(ReadOnlyMemory<byte> data, IPEndPoint ipEndpoint, CancellationToken cancellationToken)
    {
        if (!IsConnected)
        {
            throw new InvalidOperationException("Serial port is closed.");
        }

        await serialPort.BaseStream
            .WriteAsync(data, cancellationToken)
            .ConfigureAwait(false);
        logger.LogTrace("Wrote {BytesWritten} bytes to serial port {PortName}.", data.Length, serialPort.PortName);
    }

    /// <inheritdoc />
    public Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            if (serialPort.IsOpen)
            {
                serialPort.Close();
            }

            logger.LogTrace("Serial port {PortName} closed.", serialPort.PortName);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Failed to close serial port {PortName}.", serialPort.PortName);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        if (serialPort.IsOpen)
        {
            serialPort.Close();
        }

        serialPort.Dispose();
        GC.SuppressFinalize(this);
        logger.LogTrace("Serial port {PortName} disposed.", serialPort.PortName);
        return ValueTask.CompletedTask;
    }
}
