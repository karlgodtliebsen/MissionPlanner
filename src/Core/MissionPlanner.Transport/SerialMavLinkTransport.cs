using System.IO.Ports;
using Microsoft.Extensions.Logging;
using MissionPlanner.Transport.Abstractions;
using Polly;
using Polly.Retry;

namespace MissionPlanner.Transport;

/// <summary>
/// Represents a MAVLink transport that communicates over a serial port.
/// </summary>
public sealed class SerialMavLinkTransport : ISerialMavLinkTransport
{
    private readonly ILogger<SerialMavLinkTransport> logger;
    private readonly SerialPort serialPort;
    private readonly TransportEndPoint endpoint;
    private readonly ResiliencePipeline retryPipeline;

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
            throw new ArgumentException("PortName must be specified.", nameof(portName));
        }

        this.logger = logger;
        serialPort = new SerialPort(portName, baudRate, Parity.None, 8, StopBits.One) { ReadTimeout = Timeout.Infinite, WriteTimeout = Timeout.Infinite };
        endpoint = new TransportEndPoint("serial", portName);

        // Configure Polly retry pipeline for resilient serial port operations
        retryPipeline = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromMilliseconds(500),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                OnRetry = args =>
                {
                    logger.LogWarning(
                        args.Outcome.Exception,
                        "Retry attempt {AttemptNumber} of {MaxRetryAttempts} for serial port {PortName}. Waiting {RetryDelay}ms before next attempt.",
                        args.AttemptNumber,
                        3,
                        portName,
                        args.RetryDelay.TotalMilliseconds);
                    return ValueTask.CompletedTask;
                }
            })
            .Build();
    }

    /// <inheritdoc />
    public bool IsConnected => serialPort.IsOpen;

    /// <inheritdoc />
    public async Task ConnectAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!serialPort.IsOpen)
        {
            // Use Polly retry mechanism to handle transient errors when opening the serial port
            await retryPipeline.ExecuteAsync(async ct =>
            {
                // SerialPort.Open() is synchronous, wrap in Task.Run to avoid blocking
                await Task.Run(() =>
                {
                    ct.ThrowIfCancellationRequested();
                    if (!serialPort.IsOpen)
                    {
                        serialPort.Open();
                    }
                }, ct).ConfigureAwait(false);

                logger.LogInformation("Serial port {PortName} opened successfully at {BaudRate} baud.", serialPort.PortName, serialPort.BaudRate);
            }, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            logger.LogTrace("Serial port {PortName} is already open.", serialPort.PortName);
        }
    }

    /// <inheritdoc />
    public async ValueTask<TransportReceiveResult> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        if (!IsConnected)
        {
            throw new InvalidOperationException("Serial port is closed.");
        }

        var bytesRead = await serialPort.BaseStream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
        logger.LogTrace("Read {BytesRead} bytes from serial port {PortName}.", bytesRead, serialPort.PortName);
        return new TransportReceiveResult(bytesRead, endpoint);
    }

    /// <inheritdoc />
    public async ValueTask WriteAsync(ReadOnlyMemory<byte> data, TransportEndPoint endPoint, CancellationToken cancellationToken)
    {
        if (!IsConnected)
        {
            throw new InvalidOperationException("Serial port is closed.");
        }

        await serialPort.BaseStream.WriteAsync(data, cancellationToken).ConfigureAwait(false);

        logger.LogTrace("Wrote {BytesWritten} bytes to serial port {PortName}.", data.Length, serialPort.PortName);
    }


    /// <inheritdoc />
    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            if (serialPort.IsOpen)
            {
                // Discard buffers before closing to ensure clean shutdown
                serialPort.DiscardInBuffer();
                serialPort.DiscardOutBuffer();
                //do not break for exceptions here, we want to close the port even if discarding buffers fails
                serialPort.Close();

                // Give the OS time to fully release the port (Windows-specific issue)
                // This prevents "Access denied" errors when reopening quickly
                await Task.Delay(100, cancellationToken).ConfigureAwait(false);
            }

            logger.LogTrace("Serial port {PortName} closed.", serialPort.PortName);
        }
        catch (OperationCanceledException)
        {
            // Don't log cancellation as an error
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Non Fatal Error. Failed to close serial port {PortName}.", serialPort.PortName);
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        try
        {
            if (serialPort.IsOpen)
            {
                // Discard buffers before closing
                try
                {
                    serialPort.DiscardInBuffer();
                    serialPort.DiscardOutBuffer();
                }
                catch
                {
                    // Ignore errors discarding buffers during disposal
                }

                serialPort.Close();

                // Give OS time to release the port
                await Task.Delay(100).ConfigureAwait(false);
            }

            serialPort.Dispose();
            GC.SuppressFinalize(this);
            logger.LogTrace("Serial port {PortName} disposed.", serialPort.PortName);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error disposing serial port {PortName}.", serialPort.PortName);
        }
    }
}
