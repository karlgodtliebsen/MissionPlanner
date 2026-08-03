using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MissionPlanner.Firmware;
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
public sealed class TemporaryMavLinkBootloaderGateway(
    IFirmwareSerialPortFactory serialPortFactory,
    IMavLinkFrameParser frameParser,
    IMavLinkMessageDecodeHandler messageDecoder,
    IMavLinkCommandEncoder commandEncoder,
    IOptions<FirmwareOptions> options,
    ILogger<TemporaryMavLinkBootloaderGateway> logger) : ITemporaryMavLinkBootloaderGateway
{
    /// <inheritdoc />
    public async Task<bool> RebootToBootloaderAsync(
        SerialDeviceDescriptor applicationDevice,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(applicationDevice);
        frameParser.Reset();
        await using var port = await serialPortFactory.OpenAsync(
            new SerialPortOpenOptions(applicationDevice.PortName, options.Value.BootloaderBaudRate),
            cancellationToken).ConfigureAwait(false);

        var endpoint = new TransportEndPoint("temporary-firmware", applicationDevice.PortName);
        var heartbeat = await ReadMessageAsync<HeartbeatMessage>(
            port.Stream,
            endpoint,
            options.Value.TemporaryMavLinkHeartbeatTimeout,
            cancellationToken).ConfigureAwait(false);
        if (heartbeat is null)
        {
            logger.LogDebug("No MAVLink heartbeat was detected on temporary firmware port {PortName}.", applicationDevice.PortName);
            return false;
        }

        var packet = commandEncoder.EncodeCommandLong(
            heartbeat.SystemId,
            heartbeat.ComponentId,
            MavLinkCommandIds.PreflightRebootShutdown,
            [(float)RebootShutdownAction.RebootToBootloader, 0, 0, 0, 0, 0, 0]);
        await port.Stream.WriteAsync(packet, cancellationToken).ConfigureAwait(false);
        await port.Stream.FlushAsync(cancellationToken).ConfigureAwait(false);

        var acknowledgement = await ReadMessageAsync<CommandAckMessage>(
            port.Stream,
            endpoint,
            options.Value.TemporaryMavLinkCommandAckTimeout,
            cancellationToken,
            message => message.SystemId == heartbeat.SystemId &&
                       message.Command == MavLinkCommandIds.PreflightRebootShutdown).ConfigureAwait(false);

        if (acknowledgement is null)
        {
            // ArduPilot may reset the serial device before its ACK reaches the host. The command
            // was transmitted successfully; release ownership and let bootloader discovery prove
            // whether the transition occurred.
            logger.LogInformation(
                "Bootloader reboot command was sent to system {SystemId} on {PortName}; no ACK arrived before the device transition timeout.",
                heartbeat.SystemId,
                applicationDevice.PortName);
            return true;
        }

        var result = (MavResult)acknowledgement.Result;
        var accepted = result is MavResult.Accepted or MavResult.InProgress;
        logger.LogInformation(
            "Temporary bootloader reboot command on {PortName} returned {AckResult}.",
            applicationDevice.PortName,
            result);
        return accepted;
    }

    private async Task<TMessage?> ReadMessageAsync<TMessage>(
        Stream stream,
        TransportEndPoint endpoint,
        TimeSpan timeout,
        CancellationToken cancellationToken,
        Func<TMessage, bool>? predicate = null)
        where TMessage : MavLinkMessage
    {
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);
        var buffer = new byte[512];
        try
        {
            while (true)
            {
                var count = await stream.ReadAsync(buffer, timeoutSource.Token).ConfigureAwait(false);
                if (count == 0) return null;
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
