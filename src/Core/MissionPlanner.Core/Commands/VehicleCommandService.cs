using MissionPlanner.Core.DomainEvents;
using MissionPlanner.Core.Simulation;
using MissionPlanner.Core.Simulation.Abstractions;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Library.DateTime.Domain;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.MavLink.Commands;
using MissionPlanner.MavLink.Encoding;
using MissionPlanner.Shared.Models.Vehicles.Models;
using MissionPlanner.Simulation.Abstractions;

namespace MissionPlanner.Core.Commands;

/// <summary>
/// Sends safety-gated, acknowledged commands to vehicles.
/// </summary>
public sealed class VehicleCommandService(
    IVehicleRegistry registry,
    IDomainEventHub eventHub,
    IVehicleConnectionSession connectionSession,
    IMavLinkCommandEncoder encoder,
    ICommandAckTracker commandAckTracker,
    IDateTimeProvider clock,
    IVehicleCommandPolicy commandPolicy,
    IArduPilotModeCatalog modeCatalog,
    IVehicleOperationGate? operationGate = null,
    ISimulationVehicleChannelRegistry? simulationChannels = null,
    IVehicleTelemetryEventHub? telemetry = null,
    IVehicleParameterRegistry? parameters = null)
    : IVehicleCommandService
{
    private static readonly TimeSpan commandAckTimeout = TimeSpan.FromSeconds(5);
    private readonly IVehicleOperationGate operationGate = operationGate ?? new VehicleOperationGate();

    /// <inheritdoc />
    public ReceiverBindAvailability GetReceiverBindAvailability(VehicleId vehicleId)
    {
        // RC_PROTOCOLS bit 9 exclusively selects CRSF. All/automatic or mixed masks
        // cannot establish which backend is in use; a generic serial port cannot either.
        var crsf = parameters?.GetParameter(vehicleId, "RC_PROTOCOLS")?.Value == 512;
        var protocol = crsf ? "CRSF / ExpressLRS" : "Unknown";
        var capability = crsf ? "Expected" : "Unknown";
        var state = registry.GetRequired(vehicleId)?.State;
        var decision = state is null ? VehicleCommandDecision.Deny("Vehicle is not registered.")
            : commandPolicy.Evaluate(state, VehicleAction.ReceiverBind);
        return new ReceiverBindAvailability(crsf && decision.IsAllowed, protocol, capability,
            !decision.IsAllowed ? decision.Reason ?? "Binding unavailable."
            : crsf ? "CRSF is explicitly selected. FC-initiated binding is expected."
            : "Receiver bind capability is unknown. Use Bind in the ExpressLRS Lua script.");
    }

    /// <inheritdoc />
    public async Task<ReceiverBindResult> StartReceiverBindAsync(VehicleId vehicleId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var correlationId = Guid.NewGuid();
        var availability = GetReceiverBindAvailability(vehicleId);
        if (!availability.IsAvailable)
        {
            return new ReceiverBindResult(Denied(vehicleId, availability.Reason), correlationId);
        }

        if (telemetry is not null)
        {
            await telemetry.PublishAsync(new MissionPlanner.Core.Diagnostics.VehicleCommandDiagnostic(
                vehicleId, clock.UtcNow, MavLinkCommandIds.StartRxPair, correlationId,
                "ReceiverBindRequested", $"ReceiverProtocol={availability.ReceiverProtocol}; Capability={availability.Capability}; MavCommand=START_RX_PAIR")
            { ReceiverProtocol = availability.ReceiverProtocol });
        }

        var response = await ExecuteAsync(vehicleId, VehicleAction.ReceiverBind, MavLinkCommandIds.StartRxPair,
            [1, 0, 0, 0, 0, 0, 0], false, cancellationToken, transactionId: correlationId).ConfigureAwait(false);
        return new ReceiverBindResult(response with
        {
            Message = response.Result == VehicleCommandResult.Accepted
                ? "Bind command accepted/sent. This does not confirm that the receiver entered bind mode."
                : response.Message
        }, correlationId);
    }

    /// <inheritdoc />
    public Task<VehicleCommandResponse> ArmAsync(VehicleId vehicleId, CancellationToken cancellationToken)
    {
        return ExecuteAsync(vehicleId, VehicleAction.Arm, MavLinkCommandIds.ComponentArmDisarm, [1], false, cancellationToken,
            async (response, token) => await eventHub.PublishDomainEventAsync(new VehicleArmed(response.VehicleId), token));
    }

    /// <inheritdoc />
    public Task<VehicleCommandResponse> DisarmAsync(VehicleId vehicleId, CancellationToken cancellationToken)
    {
        return DisarmAsync(vehicleId, false, cancellationToken);
    }

    /// <inheritdoc />
    public Task<VehicleCommandResponse> DisarmAsync(VehicleId vehicleId, bool safetyConfirmed, CancellationToken cancellationToken)
    {
        return ExecuteAsync(vehicleId, VehicleAction.Disarm, MavLinkCommandIds.ComponentArmDisarm, [0], safetyConfirmed, cancellationToken,
            async (response, token) => await eventHub.PublishDomainEventAsync(new VehicleDisarmed(response.VehicleId), token));
    }

    /// <inheritdoc />
    public Task<VehicleCommandResponse> LandAsync(VehicleState state, CancellationToken cancellationToken)
    {
        return SetSemanticModeAsync(state.VehicleId, VehicleAction.Land, VehicleMode.Land, cancellationToken);
    }

    /// <inheritdoc />
    public Task<VehicleCommandResponse> LandAsync(VehicleId vehicleId, CancellationToken cancellationToken)
    {
        return SetSemanticModeAsync(vehicleId, VehicleAction.Land, VehicleMode.Land, cancellationToken);
    }

    /// <inheritdoc />
    public Task<VehicleCommandResponse> SetModeAsync(VehicleId vehicleId, VehicleMode mode, CancellationToken cancellationToken)
    {
        var state = registry.GetRequired(vehicleId)?.State;
        if (state is null)
        {
            return Task.FromResult(NotFound(vehicleId));
        }

        var option = modeCatalog.Find(state.Identity.Firmware.Family, mode);
        return option is null
            ? Task.FromResult(Denied(vehicleId, $"{mode} is not available for {state.Identity.Firmware.Family}."))
            : SetModeAsync(vehicleId, option, cancellationToken);
    }

    /// <inheritdoc />
    public Task<VehicleCommandResponse> SetModeAsync(VehicleId vehicleId, VehicleModeOption mode, CancellationToken cancellationToken)
    {
        var state = registry.GetRequired(vehicleId)?.State;
        return state is null
            ? Task.FromResult(NotFound(vehicleId))
            : !modeCatalog.GetModes(state.Identity.Firmware.Family).Contains(mode)
                ? Task.FromResult(Denied(vehicleId, $"{mode.Name} is not valid for {state.Identity.Firmware.Family}."))
                : ExecuteModeAsync(vehicleId, VehicleAction.SetMode, mode, cancellationToken);
    }

    /// <inheritdoc />
    public Task<VehicleCommandResponse> TakeoffAsync(VehicleId vehicleId, double altitudeMeters, bool safetyConfirmed, CancellationToken cancellationToken)
    {
        return !double.IsFinite(altitudeMeters) || altitudeMeters is < 1 or > 1000
            ? Task.FromResult(Denied(vehicleId, "Takeoff altitude must be between 1 and 1000 metres."))
            : ExecuteAsync(vehicleId, VehicleAction.Takeoff, MavLinkCommandIds.NavTakeoff,
                [0, 0, 0, 0, 0, 0, (float)altitudeMeters], safetyConfirmed, cancellationToken);
    }

    /// <inheritdoc />
    public Task<VehicleCommandResponse> ReturnToLaunchAsync(VehicleId vehicleId, CancellationToken cancellationToken)
    {
        return SetSemanticModeAsync(vehicleId, VehicleAction.ReturnToLaunch, VehicleMode.Rtl, cancellationToken);
    }

    /// <inheritdoc />
    public Task<VehicleCommandResponse> HoldAsync(VehicleId vehicleId, CancellationToken cancellationToken)
    {
        return SetSemanticModeAsync(vehicleId, VehicleAction.Hold, VehicleMode.Loiter, cancellationToken);
    }

    /// <inheritdoc />
    public Task<VehicleCommandResponse> RebootAutopilotAsync(VehicleId vehicleId, bool safetyConfirmed, CancellationToken cancellationToken)
    {
        return ExecuteAsync(vehicleId, VehicleAction.RebootAutopilot, MavLinkCommandIds.PreflightRebootShutdown,
            [1, 0, 0, 0, 0, 0, 0], safetyConfirmed, cancellationToken);
    }

    /// <inheritdoc />
    public Task<VehicleCommandResponse> SetHomeHereAsync(VehicleId vehicleId, bool safetyConfirmed, CancellationToken cancellationToken)
    {
        return ExecuteAsync(vehicleId, VehicleAction.SetHomeHere, MavLinkCommandIds.DoSetHome,
            [1, 0, 0, 0, 0, 0, 0], safetyConfirmed, cancellationToken);
    }

    /// <inheritdoc />
    public Task<VehicleCommandResponse> ExecuteExpertAsync(ExpertVehicleCommand command, bool safetyConfirmed, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        return command.CommandId == 0 || command.Parameters.Count != 7 || command.Parameters.Any(value => !float.IsFinite(value))
            ? Task.FromResult(Denied(command.VehicleId, "Expert command requires a non-zero ID and exactly seven finite parameters."))
            : command.CommandId is MavLinkCommandIds.ComponentArmDisarm or MavLinkCommandIds.DoSetMode or MavLinkCommandIds.PreflightRebootShutdown or MavLinkCommandIds.StartRxPair
                ? Task.FromResult(Denied(command.VehicleId, "Use the typed safety-aware action for arm, mode, or reboot commands."))
                : ExecuteAsync(command.VehicleId, VehicleAction.ExpertCommand, command.CommandId, command.Parameters, safetyConfirmed, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }

    private Task<VehicleCommandResponse> SetSemanticModeAsync(
        VehicleId vehicleId,
        VehicleAction action,
        VehicleMode semanticMode,
        CancellationToken cancellationToken)
    {
        var state = registry.GetRequired(vehicleId)?.State;
        if (state is null)
        {
            return Task.FromResult(NotFound(vehicleId));
        }

        var option = modeCatalog.Find(state.Identity.Firmware.Family, semanticMode);
        return option is null
            ? Task.FromResult(Denied(vehicleId, $"{semanticMode} is not available for {state.Identity.Firmware.Family}."))
            : ExecuteModeAsync(vehicleId, action, option, cancellationToken);
    }

    private Task<VehicleCommandResponse> ExecuteModeAsync(
        VehicleId vehicleId,
        VehicleAction action,
        VehicleModeOption mode,
        CancellationToken cancellationToken)
    {
        return ExecuteAsync(vehicleId, action, MavLinkCommandIds.DoSetMode, [1, mode.CustomMode], false, cancellationToken,
            async (response, token) =>
                await eventHub.PublishDomainEventAsync(
                    new VehicleModeChanged(new VehicleModeChange(response.VehicleId, mode.SemanticMode, clock.UtcNow)), token));
    }

    private async Task<VehicleCommandResponse> ExecuteAsync(
        VehicleId vehicleId,
        VehicleAction action,
        ushort commandId,
        IReadOnlyList<float> parameters,
        bool safetyConfirmed,
        CancellationToken cancellationToken,
        Func<VehicleCommandResponse, CancellationToken, Task>? onAccepted = null,
        Guid? transactionId = null)
    {
        var session = registry.GetRequired(vehicleId);
        if (session is null)
        {
            return NotFound(vehicleId);
        }

        var decision = commandPolicy.Evaluate(session.State, action);
        if (!decision.IsAllowed)
        {
            return Denied(vehicleId, decision.Reason ?? "Command denied by safety policy.");
        }

        if (decision.RequiresConfirmation && !safetyConfirmed)
        {
            return Denied(vehicleId, decision.Reason ?? "Explicit confirmation is required.");
        }

        if (!operationGate.TryAcquire(vehicleId, $"command {commandId}", out var operationLease))
        {
            return new VehicleCommandResponse(vehicleId, VehicleCommandResult.Busy, clock.UtcNow,
                $"Another operation is already pending for this vehicle ({operationGate.GetCurrentOperation(vehicleId)}).");
        }

        using (operationLease)
        {
            var packet = encoder.EncodeCommandLong(vehicleId.SystemId, vehicleId.ComponentId, commandId, parameters);
            var targetSession = simulationChannels?.Find(vehicleId)?.ConnectionSession ?? connectionSession;
            var targetConnection = targetSession.Connection;
            var connectionToken = targetConnection.Activity?.LifetimeToken ?? targetSession.ConnectionCancellationToken;
            using var ackLifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, connectionToken);
            var correlationId = transactionId ?? Guid.NewGuid();
            var waitForAck = commandAckTracker.WaitForAckAsync(vehicleId, commandId, commandAckTimeout, ackLifetime.Token);

            try
            {
                ackLifetime.Token.ThrowIfCancellationRequested();
                await targetConnection.SendRawAsync(packet, session.EndPoint, ackLifetime.Token).ConfigureAwait(false);
                if (telemetry is not null)
                {
                    await telemetry.PublishAsync(new MissionPlanner.Core.Diagnostics.VehicleCommandDiagnostic(
                        vehicleId, clock.UtcNow, commandId, correlationId, "TX",
                        $"Command {commandId}: {string.Join(", ", parameters)}", commandId == 400 && parameters[0] == 1));
                }
                var ack = await waitForAck.ConfigureAwait(false);
                if (telemetry is not null)
                {
                    await telemetry.PublishAsync(new MissionPlanner.Core.Diagnostics.VehicleCommandDiagnostic(
                        vehicleId, ack.ReceivedAt, commandId, correlationId, "ACK",
                        $"Command {commandId}: {MapResult(ack.Result)}; CommandAck={ack.Result}; VehicleReason={ack.ResultParameter2}", commandId == 400 && parameters[0] == 1)
                    { CommandAck = ack.Result, VehicleReason = ack.ResultParameter2 });
                }
                var response = new VehicleCommandResponse(vehicleId, MapResult(ack.Result), ack.ReceivedAt,
                    $"MAVLink ACK result {ack.Result}.");
                if (response.Result == VehicleCommandResult.Accepted && onAccepted is not null)
                {
                    await onAccepted(response, cancellationToken).ConfigureAwait(false);
                }

                return response;
            }
            catch (OperationCanceledException) when (connectionToken.IsCancellationRequested)
            {
                return new VehicleCommandResponse(vehicleId, VehicleCommandResult.ConnectionLost, clock.UtcNow,
                    "Connection lost while waiting for command acknowledgement.");
            }
            catch (TimeoutException)
            {
                return new VehicleCommandResponse(vehicleId, VehicleCommandResult.Timeout, clock.UtcNow,
                    "No command acknowledgement was received before the timeout.");
            }
            finally
            {
                await ackLifetime.CancelAsync().ConfigureAwait(false);
            }
        }
    }

    private VehicleCommandResponse NotFound(VehicleId vehicleId)
    {
        return new VehicleCommandResponse(vehicleId, VehicleCommandResult.VehicleNotFound, clock.UtcNow, "Vehicle is not registered.");
    }

    private VehicleCommandResponse Denied(VehicleId vehicleId, string reason)
    {
        return new VehicleCommandResponse(vehicleId, VehicleCommandResult.Denied, clock.UtcNow, reason);
    }

    private static VehicleCommandResult MapResult(byte mavResult)
    {
        return mavResult switch
        {
            0 => VehicleCommandResult.Accepted,
            1 => VehicleCommandResult.TemporarilyRejected,
            2 => VehicleCommandResult.Denied,
            3 => VehicleCommandResult.Unsupported,
            4 => VehicleCommandResult.Failed,
            var _ => VehicleCommandResult.Failed
        };
    }
}
