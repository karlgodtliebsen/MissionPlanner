using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MissionPlanner.Firmware.Configuration;
using MissionPlanner.Firmware.Devices;
using MissionPlanner.Firmware.Entry;
using MissionPlanner.Firmware.Model;
using MissionPlanner.MavLink.Commands;
using MissionPlanner.MavLink.Encoding;
using MissionPlanner.MavLink.Generated;
using MissionPlanner.MavLink.Messages;
using MissionPlanner.MavLink.Services.Abstractions;
using MissionPlanner.Transport;

namespace MissionPlanner.Core.Firmware;

/// <summary>
/// Uses a one-shot isolated serial stream to request bootloader reboot without creating a
/// Mission Planner vehicle session or publishing messages into the application event hub.
/// </summary>
// </summary>
public sealed class TemporaryMavLinkBootloaderGateway(
    IFirmwareSerialPortFactory serialPortFactory,
    IMavLinkFrameParser frameParser,
    IMavLinkMessageDecodeHandler messageDecoder,
    IMavLinkCommandEncoder commandEncoder,
    IOptions<FirmwareOptions> options,
    ILogger<TemporaryMavLinkBootloaderGateway> logger) : ITemporaryMavLinkBootloaderGateway
{
    /// <inheritdoc />
    public async Task<bool> RebootToBootloaderAsync(SerialDeviceDescriptor applicationDevice, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(applicationDevice);
        frameParser.Reset();

        await using var port = await serialPortFactory.OpenAsync(new SerialPortOpenOptions(applicationDevice.PortName, options.Value.BootloaderBaudRate), cancellationToken).ConfigureAwait(false);
        var endpoint = new TransportEndPoint("temporary-firmware", applicationDevice.PortName);

        var heartbeat = await ReadMessageAsync<HeartbeatMessage>(port.Stream, endpoint, options.Value.TemporaryMavLinkHeartbeatTimeout, cancellationToken).ConfigureAwait(false);
        if (heartbeat is null)
        {
            logger.LogDebug("No MAVLink heartbeat was detected on temporary firmware port {PortName}.", applicationDevice.PortName);
            return false;
        }

        var packet = commandEncoder.EncodeCommandLong(heartbeat.SystemId, heartbeat.ComponentId, MavLinkCommandIds.PreflightRebootShutdown, [
            (float)RebootShutdownAction.RebootToBootloader, 0, 0, 0, 0, 0, 0
        ]);

        await port.Stream.WriteAsync(packet, cancellationToken).ConfigureAwait(false);
        await port.Stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        // Do not wait for COMMAND_ACK here. ArduPilot commonly resets the USB serial device
        // before sending it, and SerialPort.BaseStream.ReadAsync can leave a native Windows read
        // pending after a managed timeout. That pending read retains exclusive ownership of the
        // COM port and prevents the bootloader discovery service from opening it. A successful
        // protocol handshake by discovery is the authoritative confirmation of the reboot.
        logger.LogInformation(
            "Bootloader reboot command was sent to system {SystemId} on {PortName}; releasing the temporary port immediately for discovery.",
            heartbeat.SystemId,
            applicationDevice.PortName);
        return true;
    }

    private async Task<TMessage?> ReadMessageAsync<TMessage>(Stream stream, TransportEndPoint endpoint, TimeSpan timeout, CancellationToken cancellationToken, Func<TMessage, bool>? predicate = null)
        where TMessage : MavLinkMessage
    {
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);
        var buffer = new byte[512];
        try
        {
            while (true)
            {
                // SerialPort.BaseStream on Windows may ignore ReadAsync cancellation. Enforce
                // the bounded MAVLink wait independently; disposing the owned port releases the
                // outstanding native read after this method returns.
                var count = await stream.ReadAsync(buffer, CancellationToken.None)
                    .AsTask()
                    .WaitAsync(timeoutSource.Token)
                    .ConfigureAwait(false);
                if (count == 0)
                {
                    return null;
                }

                foreach (var frame in frameParser.Parse(buffer.AsSpan(0, count), endpoint, DateTimeOffset.UtcNow))
                {
                    if (messageDecoder.TryDecode(frame, out var decoded) &&
                        decoded is TMessage message &&
                        (predicate is null || predicate(message)))
                    {
                        return message;
                    }
                }
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
        catch (IOException exception)
        {
            logger.LogDebug(exception, "Temporary MAVLink serial stream closed during bootloader transition.");
            return null;
        }
    }
}
