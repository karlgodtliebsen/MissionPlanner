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
using MissionPlanner.Firmware.Betaflight;
using MissionPlanner.Firmware.Workflow;

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
    ILogger<TemporaryMavLinkBootloaderGateway> logger) : ITemporaryMavLinkBootloaderGateway, IArduPilotRuntimeVerifier
{

    /// <inheritdoc />
    public async Task<FirmwareRuntimeProbeResult> ProbeAsync(SerialDeviceDescriptor device, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        frameParser.Reset();
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(options.Value.BootloaderPortOpenTimeout + options.Value.TemporaryMavLinkHeartbeatTimeout + TimeSpan.FromSeconds(2));
        try
        {
            await using var port = await serialPortFactory.OpenAsync(new SerialPortOpenOptions(device.PortName,
                options.Value.BootloaderBaudRate), deadline.Token).ConfigureAwait(false);
            var heartbeat = await ReadMessageAsync<HeartbeatMessage>(port.Stream,
                new TransportEndPoint("firmware-runtime-probe", device.PortName), options.Value.TemporaryMavLinkHeartbeatTimeout,
                deadline.Token, message => message.ComponentId == 1, propagateTransportError: true).ConfigureAwait(false);
            if (heartbeat is null)
            {
                return Failed(FirmwareRuntimeProbeOutcome.Timeout, "runtime.mavlink-timeout");
            }
            if (heartbeat.Autopilot != 3)
            {
                return Failed(FirmwareRuntimeProbeOutcome.OtherAutopilot, "runtime.other-autopilot");
            }
            var running = await ReadRunningIdentityAsync(port.Stream, heartbeat, device, deadline.Token).ConfigureAwait(false);
            return new(FirmwareRuntimeKind.ArduPilot, FirmwareBootEnvironment.None, "runtime.ardupilot", (heartbeat.BaseMode & 128) != 0)
            {
                RunningIdentity = running,
                Evidence = FirmwareRuntimeEvidence.MavLinkProbe,
                Verification = FirmwareRuntimeVerification.Verified,
                Outcome = FirmwareRuntimeProbeOutcome.Success
            };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Failed(FirmwareRuntimeProbeOutcome.Timeout, "runtime.mavlink-timeout");
        }
        catch (UnauthorizedAccessException)
        {
            return Failed(FirmwareRuntimeProbeOutcome.PortBusy, "runtime.port-busy");
        }
        catch (Exception exception) when (exception is IOException or TimeoutException)
        {
            return Failed(FirmwareRuntimeProbeOutcome.TransportError, "runtime.transport-error");
        }

        static FirmwareRuntimeProbeResult Failed(FirmwareRuntimeProbeOutcome outcome, string code)
        {
            return new(FirmwareRuntimeKind.Unknown, FirmwareBootEnvironment.None, code) { Outcome = outcome };
        }
    }

    /// <inheritdoc />
    public async Task<ArduPilotRuntimeIdentity?> VerifyAsync(SerialDeviceDescriptor device, CancellationToken cancellationToken = default)
    {
        frameParser.Reset();
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(options.Value.BootloaderPortOpenTimeout + options.Value.TemporaryMavLinkHeartbeatTimeout + TimeSpan.FromSeconds(2));
        try
        {
            await using var port = await serialPortFactory.OpenAsync(new SerialPortOpenOptions(device.PortName,
                options.Value.BootloaderBaudRate), deadline.Token).ConfigureAwait(false);
            var heartbeat = await ReadMessageAsync<HeartbeatMessage>(port.Stream,
                new TransportEndPoint("firmware-runtime-probe", device.PortName), options.Value.TemporaryMavLinkHeartbeatTimeout,
                deadline.Token, message => message.Autopilot == 3 && message.ComponentId == 1).ConfigureAwait(false);
            return heartbeat is null ? null : new(heartbeat.SystemId, heartbeat.ComponentId, null, (heartbeat.BaseMode & 128) != 0);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or TimeoutException)
        {
            logger.LogDebug("Runtime probe unavailable on {PortName}: {Reason}", device.PortName, exception.Message);
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<bool> RebootToBootloaderAsync(SerialDeviceDescriptor applicationDevice, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(applicationDevice);
        cancellationToken.ThrowIfCancellationRequested();
        frameParser.Reset();

        logger.LogInformation("Attempting temporary MAVLink bootloader reboot on application serial endpoint {PortName} ({DeviceIdentity}).",
            applicationDevice.PortName, applicationDevice.StableIdentity);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(options.Value.BootloaderPortOpenTimeout + options.Value.TemporaryMavLinkHeartbeatTimeout + options.Value.BootloaderCommandTimeout);
        await using var port = await serialPortFactory.OpenAsync(new SerialPortOpenOptions(applicationDevice.PortName, options.Value.BootloaderBaudRate), deadline.Token).ConfigureAwait(false);
        var endpoint = new TransportEndPoint("temporary-firmware", applicationDevice.PortName);

        var heartbeat = await ReadMessageAsync<HeartbeatMessage>(port.Stream, endpoint, options.Value.TemporaryMavLinkHeartbeatTimeout, cancellationToken,
            message => message.Autopilot == 3 && message.ComponentId == 1).ConfigureAwait(false);
        if (heartbeat is null)
        {
            logger.LogDebug("No MAVLink heartbeat was detected on temporary firmware port {PortName}.", applicationDevice.PortName);
            return false;
        }

        if ((heartbeat.BaseMode & 128) != 0)
        {
            logger.LogWarning("Refusing bootloader entry for armed controller on {PortName}.", applicationDevice.PortName);
            return false;
        }

        var packet = commandEncoder.EncodeCommandLong(heartbeat.SystemId, heartbeat.ComponentId, MavLinkCommandIds.PreflightRebootShutdown, [
            (float)RebootShutdownAction.RebootToBootloader, 0, 0, 0, 0, 0, 0
        ]);

        try
        {
            await port.Stream.WriteAsync(packet, deadline.Token).AsTask().WaitAsync(deadline.Token).ConfigureAwait(false);
            await port.Stream.FlushAsync(deadline.Token).WaitAsync(deadline.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException("Temporary MAVLink reboot write timed out; bootloader discovery must determine whether the device reset.");
        }
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

    private async Task<RunningFirmwareIdentity?> ReadRunningIdentityAsync(Stream stream, HeartbeatMessage heartbeat,
        SerialDeviceDescriptor device, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(2));
        var identity = Vehicles.Models.VehicleFirmwareIdentityFactory.FromHeartbeat(heartbeat.VehicleType, heartbeat.Autopilot);
        var text = new List<string>();
        try
        {
            foreach (var command in new[] { (ushort)MavCmd.RequestAutopilotCapabilities, (ushort)MavCmd.DoSendBanner })
            {
                var packet = commandEncoder.EncodeCommandLong(heartbeat.SystemId, heartbeat.ComponentId, command, [1, 0, 0, 0, 0, 0, 0]);
                await stream.WriteAsync(packet, timeout.Token).AsTask().WaitAsync(timeout.Token).ConfigureAwait(false);
            }
            var buffer = new byte[1024];
            while (identity.FlightVersion is null || !text.Any(value => RunningFirmwareIdentity.ParseTarget(value) is not null))
            {
                var count = await stream.ReadAsync(buffer, CancellationToken.None).AsTask().WaitAsync(timeout.Token).ConfigureAwait(false);
                if (count == 0)
                {
                    break;
                }
                foreach (var frame in frameParser.Parse(buffer.AsSpan(0, count),
                    new TransportEndPoint("firmware-identity", device.PortName), DateTimeOffset.UtcNow))
                {
                    if (!messageDecoder.TryDecode(frame, out var decoded) || decoded is null ||
                        decoded.SystemId != heartbeat.SystemId || decoded.ComponentId != heartbeat.ComponentId)
                    {
                        continue;
                    }
                    if (decoded is AutopilotVersionMessage version)
                    {
                        identity = identity with
                        {
                            FlightVersion = MissionPlanner.Firmware.FirmwareSemanticVersion.FromPacked(version.FlightSoftwareVersion),
                            FlightGitHash = Convert.ToHexString(version.FlightCustomVersion).ToLowerInvariant(),
                            BoardVersion = version.BoardVersion,
                            HardwareUid = version.Uid == 0 ? null : version.Uid,
                            HardwareUid2 = version.Uid2.Any(value => value != 0) ? Convert.ToHexString(version.Uid2).ToLowerInvariant() : null
                        };
                    }
                    else if (decoded is StatusTextMessage status && status.IsTextTerminated && status.ChunkSequence is null or 0)
                    {
                        if (text.Count < 64)
                        {
                            text.Add(status.Text);
                        }
                    }
                }
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Partial identity is still attributed; absence must not become a fabricated target.
        }
        return RunningFirmwareIdentity.FromTelemetry(identity, text);
    }

    private async Task<TMessage?> ReadMessageAsync<TMessage>(Stream stream, TransportEndPoint endpoint, TimeSpan timeout, CancellationToken cancellationToken,
        Func<TMessage, bool>? predicate = null, bool propagateTransportError = false)
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
        catch (IOException exception) when (!propagateTransportError)
        {
            logger.LogDebug(exception, "Temporary MAVLink serial stream closed during bootloader transition.");
            return null;
        }
    }
}
