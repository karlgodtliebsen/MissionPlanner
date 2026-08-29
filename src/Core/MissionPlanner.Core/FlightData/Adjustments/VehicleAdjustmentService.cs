using MissionPlanner.Core.Commands;
using MissionPlanner.Core.DomainEvents;
using MissionPlanner.Core.Simulation.Abstractions;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Firmware;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.MavLink.Encoding;
using MissionPlanner.MavLink.Generated;
using MissionPlanner.MavLink.Messages;
using MissionPlanner.MavLink.Services.Abstractions;
using MissionPlanner.Shared.Models.Vehicles.Models;
using MissionPlanner.Simulation.Abstractions;

namespace MissionPlanner.Core.FlightData.Adjustments;

public sealed class VehicleAdjustmentService(
    IVehicleRegistry registry,
    IVehicleConnectionSession connectionSession,
    IMavLinkCommandEncoder commandEncoder,
    IMavLinkWireMessageEncoder wireEncoder,
    ICommandAckTracker ackTracker,
    IVehicleOperationGate operationGate,
    IDomainEventHub domainEventHub,
    IVehicleParameterRegistry parameterRegistry,
    IVehicleParameterService parameterService,
    ISimulationVehicleChannelRegistry? simulationChannels = null) : IVehicleAdjustmentService
{
    private static readonly TimeSpan AckTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan ConfirmationTimeout = TimeSpan.FromSeconds(5);
    private const double AltitudeToleranceMeters = 0.75;

    public VehicleCommandDecision EvaluateSpeed(VehicleState state, VehicleSpeedTargetType targetType)
    {
        if (state.ConnectionState != VehicleConnectionState.Online) return VehicleCommandDecision.Deny("Vehicle is not online.");
        var family = state.Identity.Firmware.Family;
        if (targetType == VehicleSpeedTargetType.Airspeed && family != FirmwareFamily.ArduPlane)
            return VehicleCommandDecision.Deny("Airspeed targets are supported only for Plane.");
        if (family is not (FirmwareFamily.ArduCopter or FirmwareFamily.ArduPlane or FirmwareFamily.Rover))
            return VehicleCommandDecision.Deny("Speed adjustment is unsupported for this vehicle family.");
        return IsSpeedMode(family, state.CustomMode)
            ? VehicleCommandDecision.Allow()
            : VehicleCommandDecision.Deny("The current mode does not accept an external speed target.");
    }

    public VehicleCommandDecision EvaluateAltitude(VehicleState state)
    {
        if (state.ConnectionState != VehicleConnectionState.Online) return VehicleCommandDecision.Deny("Vehicle is not online.");
        if (state.Identity.Firmware.Family is not (FirmwareFamily.ArduCopter or FirmwareFamily.ArduPlane))
            return VehicleCommandDecision.Deny("Guided altitude is available only for Copter and Plane.");
        if (!IsGuided(state.Identity.Firmware.Family, state.CustomMode)) return VehicleCommandDecision.Deny("Vehicle must already be in Guided mode.");
        return state.Position.LatitudeDegrees is not null && state.Position.LongitudeDegrees is not null
            ? VehicleCommandDecision.Allow()
            : VehicleCommandDecision.Deny("A current vehicle position is required.");
    }

    public VehicleCommandDecision EvaluateLoiterRadius(VehicleState state)
    {
        if (state.ConnectionState != VehicleConnectionState.Online) return VehicleCommandDecision.Deny("Vehicle is not online.");
        return SelectRadiusParameter(state.VehicleId) is null
            ? VehicleCommandDecision.Deny("WP_LOITER_RAD/LOITER_RAD is not available.")
            : VehicleCommandDecision.Allow(true, "This writes a persistent vehicle parameter.");
    }

    public async Task<VehicleAdjustmentResult> ChangeSpeedAsync(VehicleId vehicleId, VehicleSpeedTargetType targetType, double metersPerSecond, CancellationToken cancellationToken)
    {
        var session = registry.GetRequired(vehicleId);
        var state = session?.State;
        if (state is null) return Denied(vehicleId, "Vehicle is not connected.");
        var decision = EvaluateSpeed(state, targetType);
        if (!decision.IsAllowed) return Denied(vehicleId, decision.Reason!);
        if (!double.IsFinite(metersPerSecond) || metersPerSecond <= 0) return Denied(vehicleId, "Speed must be a positive finite value.");
        if (!operationGate.TryAcquire(vehicleId, "change speed", out var lease)) return Busy(vehicleId);
        using (lease)
        using (var ackLifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
        {
            var command = (ushort)MavCmd.DoChangeSpeed;
            var ackWait = ackTracker.WaitForAckAsync(vehicleId, command, AckTimeout, ackLifetime.Token);
            try
            {
                var speedType = targetType == VehicleSpeedTargetType.Airspeed ? 0f : 1f;
                var packet = commandEncoder.EncodeCommandLong(vehicleId.SystemId, vehicleId.ComponentId, command,
                    [speedType, (float)metersPerSecond, -1, 0, 0, 0, 0]);
                await GetConnection(vehicleId).SendRawAsync(packet, session!.EndPoint, cancellationToken).ConfigureAwait(false);
                var ack = await ackWait.ConfigureAwait(false);
                return ack.Result switch
                {
                    0 => new(vehicleId, VehicleAdjustmentStatus.CommandAccepted, "Speed command accepted by the vehicle."),
                    3 => new(vehicleId, VehicleAdjustmentStatus.Unsupported, "Speed command is unsupported."),
                    _ => new(vehicleId, VehicleAdjustmentStatus.Failed, $"Speed command rejected with MAV_RESULT {ack.Result}.")
                };
            }
            catch (TimeoutException) { return new(vehicleId, VehicleAdjustmentStatus.Timeout, "Speed command acknowledgement timed out."); }
            finally { await ackLifetime.CancelAsync().ConfigureAwait(false); }
        }
    }

    public async Task<VehicleAdjustmentResult> SetGuidedAltitudeAsync(VehicleId vehicleId, double homeRelativeMeters, CancellationToken cancellationToken)
    {
        var session = registry.GetRequired(vehicleId);
        var state = session?.State;
        if (state is null) return Denied(vehicleId, "Vehicle is not connected.");
        var decision = EvaluateAltitude(state);
        if (!decision.IsAllowed) return Denied(vehicleId, decision.Reason!);
        if (!double.IsFinite(homeRelativeMeters) || homeRelativeMeters < 0) return Denied(vehicleId, "Altitude must be a finite non-negative HOME-relative target.");
        if (!operationGate.TryAcquire(vehicleId, "guided altitude", out var lease)) return Busy(vehicleId);
        using (lease)
        {
            var confirmation = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using var subscription = domainEventHub.SubscribeDomainEventAsync<VehicleStateUpdated>((evt, _) =>
            {
                if (evt.VehicleId == vehicleId && evt.VehicleState.Position.RelativeAltitudeMeters is { } altitude && Math.Abs(altitude - homeRelativeMeters) <= AltitudeToleranceMeters)
                    confirmation.TrySetResult();
                return Task.CompletedTask;
            });
            var mask = PositionTargetTypemask.VxIgnore | PositionTargetTypemask.VyIgnore | PositionTargetTypemask.VzIgnore |
                       PositionTargetTypemask.AxIgnore | PositionTargetTypemask.AyIgnore | PositionTargetTypemask.AzIgnore |
                       PositionTargetTypemask.YawIgnore | PositionTargetTypemask.YawRateIgnore;
            var message = new SetPositionTargetGlobalIntMessage(255, 190, session!.EndPoint,
                0, vehicleId.SystemId, vehicleId.ComponentId, (byte)MavFrame.GlobalRelativeAltInt, (ushort)mask,
                checked((int)Math.Round(state.Position.LatitudeDegrees!.Value * 1e7)), checked((int)Math.Round(state.Position.LongitudeDegrees!.Value * 1e7)),
                (float)homeRelativeMeters, 0, 0, 0, 0, 0, 0, 0, 0, DateTimeOffset.UtcNow);
            await GetConnection(vehicleId).SendRawAsync(wireEncoder.Encode(message), session.EndPoint, cancellationToken).ConfigureAwait(false);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(ConfirmationTimeout);
            try
            {
                await confirmation.Task.WaitAsync(timeout.Token).ConfigureAwait(false);
                return new(vehicleId, VehicleAdjustmentStatus.TelemetryConfirmed, "Guided HOME-relative altitude reached within tolerance.");
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return new(vehicleId, VehicleAdjustmentStatus.TargetSentButNotTelemetryConfirmed, "Guided altitude target sent; target altitude was not observed before timeout.");
            }
        }
    }

    public async Task<VehicleAdjustmentResult> SetLoiterRadiusAsync(VehicleId vehicleId, double magnitudeMeters, CancellationToken cancellationToken)
    {
        var state = registry.GetRequired(vehicleId)?.State;
        if (state is null) return Denied(vehicleId, "Vehicle is not connected.");
        var selected = SelectRadiusParameter(vehicleId);
        if (selected is null) return new(vehicleId, VehicleAdjustmentStatus.Unsupported, "No supported loiter-radius parameter is available.");
        if (!double.IsFinite(magnitudeMeters) || magnitudeMeters <= 0) return Denied(vehicleId, "Loiter radius must be a positive finite magnitude.");
        if (!operationGate.TryAcquire(vehicleId, "set loiter radius", out var lease)) return Busy(vehicleId);
        using (lease)
        {
            var signed = (float)(selected.Value.Value < 0 ? -magnitudeMeters : magnitudeMeters);
            var confirmed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            void Changed(VehicleParameterChangedEventArgs args)
            {
                if (args.VehicleId == vehicleId && args.Parameter?.Name == selected.Value.Name && Math.Abs(args.Parameter.Value - signed) < 0.001f)
                    confirmed.TrySetResult();
            }
            parameterRegistry.Changed += Changed;
            try
            {
                if (!await parameterService.SetParameterAsync(vehicleId, selected.Value.Name, signed, selected.Value.Type, cancellationToken).ConfigureAwait(false))
                    return new(vehicleId, VehicleAdjustmentStatus.Failed, "Parameter write could not be sent.");
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(ConfirmationTimeout);
                try
                {
                    await confirmed.Task.WaitAsync(timeout.Token).ConfigureAwait(false);
                    return new(vehicleId, VehicleAdjustmentStatus.ParameterConfirmed, $"Persistent {selected.Value.Name} confirmed at {signed:0.##} m.", signed);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    return new(vehicleId, VehicleAdjustmentStatus.Timeout, "Parameter value confirmation timed out.");
                }
            }
            finally { parameterRegistry.Changed -= Changed; }
        }
    }

    private (string Name, float Value, MissionPlanner.MavLink.Parameters.MavParamType Type)? SelectRadiusParameter(VehicleId vehicleId)
    {
        var value = parameterRegistry.GetParameter(vehicleId, "WP_LOITER_RAD") ?? parameterRegistry.GetParameter(vehicleId, "LOITER_RAD");
        return value is null ? null : (value.Name, value.Value, value.Type);
    }

    private static bool IsGuided(FirmwareFamily family, uint mode) => family switch
    {
        FirmwareFamily.ArduCopter => mode == 4,
        FirmwareFamily.ArduPlane => mode == 15,
        _ => false
    };

    private static bool IsSpeedMode(FirmwareFamily family, uint mode) => family switch
    {
        FirmwareFamily.ArduCopter => mode is 3 or 4 or 5,
        FirmwareFamily.ArduPlane => mode is 10 or 12 or 15,
        FirmwareFamily.Rover => mode is 10 or 15,
        _ => false
    };

    private IMavLinkConnection GetConnection(VehicleId vehicleId) => simulationChannels?.Find(vehicleId)?.ConnectionSession.Connection ?? connectionSession.Connection;
    private static VehicleAdjustmentResult Denied(VehicleId id, string message) => new(id, VehicleAdjustmentStatus.Denied, message);
    private VehicleAdjustmentResult Busy(VehicleId id) => new(id, VehicleAdjustmentStatus.Busy, $"Another operation is pending ({operationGate.GetCurrentOperation(id)}).");
}
