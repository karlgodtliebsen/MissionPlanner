using Microsoft.Extensions.Logging;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Firmware.Exceptions;
using MissionPlanner.Firmware.Installation;
using MissionPlanner.Firmware.Model;

namespace MissionPlanner.Core.Firmware;

/// <summary>Releases the selected serial session and restores telemetry after firmware upload.</summary>
public sealed class FirmwareUpgradeConnection(IActiveVehicleContext activeVehicle, IVehicleConnectionService connection,
    IVehicleConnectionSession session, IVehicleRegistry registry, IVehicleMessageStore messages,
    ILogger<FirmwareUpgradeConnection> logger) : IFirmwareUpgradeConnection
{
    /// <inheritdoc />
    public Task<VehicleFirmwareIdentity> ReleaseAsync(SerialDeviceDescriptor device, FirmwareManifestEntry release,
        CancellationToken cancellationToken)
    {
        return ReleaseCoreAsync(device, SelectedFirmwareIdentity.FromRelease(release), cancellationToken);
    }

    /// <inheritdoc />
    public Task<VehicleFirmwareIdentity> ReleaseAsync(SerialDeviceDescriptor device, SelectedFirmwareIdentity selected,
        CancellationToken cancellationToken)
    {
        return ReleaseCoreAsync(device, selected, cancellationToken);
    }

    /// <inheritdoc />
    public Task<VehicleFirmwareIdentity> ReleaseForRecoveryAsync(SerialDeviceDescriptor device, CancellationToken cancellationToken)
    {
        return ReleaseCoreAsync(device, null, cancellationToken);
    }

    /// <inheritdoc />
    public RunningFirmwareIdentity? ReadRunningIdentity(SerialDeviceDescriptor device)
    {
        return activeVehicle.VehicleId is not { } id || activeVehicle.State is not { } state ||
            !activeVehicle.IsOnline || !string.Equals(session.ActiveSerialPort, device.PortName, StringComparison.OrdinalIgnoreCase)
            ? null
            : RunningFirmwareIdentity.FromTelemetry(state.Identity.Firmware,
            messages.GetMessages(id).Where(message => !message.IsTruncated && message.SourceComponentId == id.ComponentId)
                .Select(message => message.Text));
    }

    private async Task<VehicleFirmwareIdentity> ReleaseCoreAsync(SerialDeviceDescriptor device, SelectedFirmwareIdentity? release,
        CancellationToken cancellationToken)
    {
        var id = activeVehicle.VehicleId;
        var state = activeVehicle.State;
        if (id is null || state is null || !activeVehicle.IsOnline || state.IsArmed ||
            !string.Equals(session.ActiveTransportProtocol, "serial", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(session.ActiveSerialPort, device.PortName, StringComparison.OrdinalIgnoreCase))
        {
            throw new FirmwareConnectionConflictException("The selected controller must be the connected, disarmed serial vehicle.");
        }
        var identity = state.Identity.Firmware;
        if (release is not null)
        {
            FirmwareUpgradeVerification.Validate(identity, release, false);
            CheckReportedTarget(id.Value, release, DateTimeOffset.MinValue);
        }
        logger.LogInformation("FirmwareInstallStrategy={FirmwareInstallStrategy} VehicleId={VehicleId} OriginalPort={OriginalPort} RunningFirmwareIdentity={RunningFirmwareIdentity} SelectedFirmwareIdentity={SelectedFirmwareIdentity}",
            "ArduPilotBootloader", id, device.PortName, identity, release);
        return !await connection.ReleaseForFirmwareUpgradeAsync(id.Value, device.PortName, cancellationToken).ConfigureAwait(false)
            ? throw new FirmwareConnectionConflictException("The selected connection changed before firmware handoff.")
            : identity;
    }

    /// <inheritdoc />
    public Task<VehicleFirmwareIdentity> ReconnectAsync(SerialDeviceDescriptor device, FirmwareManifestEntry release,
        VehicleFirmwareIdentity? original, CancellationToken cancellationToken)
    {
        return ReconnectAsync(device, SelectedFirmwareIdentity.FromRelease(release), original, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<VehicleFirmwareIdentity> ReconnectAsync(SerialDeviceDescriptor device, SelectedFirmwareIdentity release,
        VehicleFirmwareIdentity? original, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(30));
        try
        {
            var started = DateTimeOffset.UtcNow;
            MissionPlanner.Core.Vehicles.VehicleConnectionResult result;
            while (true)
            {
                deadline.Token.ThrowIfCancellationRequested();
                result = await connection.ConnectSerialExclusiveAsync(device.PortName, 115200, deadline.Token).ConfigureAwait(false);
                logger.LogInformation("Firmware reconnect on {ReconnectPort}: {ReconnectResult}", device.PortName, result);
                if (result.Success && result.VehicleId is not null)
                {
                    break;
                }
                if (connection.IsConnected)
                {
                    throw new FirmwareVerificationException("Another connection became active before the flashed controller could reconnect.");
                }
                // USB enumeration can precede application startup. Retry only this matched endpoint.
                await Task.Delay(500, deadline.Token).ConfigureAwait(false);
            }
            var id = result.VehicleId!.Value;
            VehicleFirmwareIdentity? identity;
            while ((identity = registry.GetRequired(id)?.State.Identity.Firmware)?.FlightVersion is null ||
                !messages.GetMessages(id).Any(message => message.ReceivedAt >= started &&
                    message.SourceComponentId == id.ComponentId && !message.IsTruncated &&
                    RunningFirmwareIdentity.ParseTarget(message.Text) is not null))
            {
                await Task.Delay(100, deadline.Token).ConfigureAwait(false);
            }
            FirmwareUpgradeVerification.Validate(identity, release, true, original);
            CheckReportedTarget(id, release, started);
            logger.LogInformation("Firmware verification succeeded on {ReconnectPort}: {PostFlashFirmwareIdentity}", device.PortName, identity);
            return identity;
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new FirmwareVerificationException("The flashed controller did not report its running firmware before the reconnect deadline.", exception);
        }
    }


    private void CheckReportedTarget(MissionPlanner.Shared.Models.Vehicles.Models.VehicleId id,
        SelectedFirmwareIdentity release, DateTimeOffset since)
    {
        var targets = messages.GetMessages(id)
            .Where(message => message.ReceivedAt >= since && !message.IsTruncated && message.SourceComponentId == id.ComponentId)
            .Select(message => message.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Where(parts => parts.Length == 4 && parts.Skip(1).All(part => part.Length == 8 && part.All(Uri.IsHexDigit)))
            .Select(parts => parts[0]).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (targets.Any(target => !string.Equals(target, release.Target, StringComparison.OrdinalIgnoreCase)))
        {
            throw new FirmwareCompatibilityException("Reported controller target differs from the selected platform. Use explicit recovery.");
        }
    }
}
