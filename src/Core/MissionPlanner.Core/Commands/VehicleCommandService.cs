using MissionPlanner.Core.DomainEvents;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Library.DateTime.Domain;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.MavLink.Commands;
using MissionPlanner.MavLink.Encoding;
using MissionPlanner.MavLink.Services.Abstractions;

namespace MissionPlanner.Core.Commands;

/// <summary>
/// Sends safety-gated, acknowledged commands to vehicles.
/// </summary>
public sealed class VehicleCommandService(
    IVehicleRegistry registry,
    IDomainEventHub eventHub,
    IMavLinkConnection connection,
    IMavLinkCommandEncoder encoder,
    ICommandAckTracker commandAckTracker,
    IDateTimeProvider clock,
    IVehicleCommandPolicy commandPolicy,
    IArduPilotModeCatalog modeCatalog,
    IVehicleOperationGate? operationGate = null)
    : IVehicleCommandService
{
    private static readonly TimeSpan commandAckTimeout = TimeSpan.FromSeconds(5);
    private readonly IVehicleOperationGate operationGate = operationGate ?? new VehicleOperationGate();

    /// <inheritdoc />
    public Task<VehicleCommandResponse> ArmAsync(VehicleId vehicleId, CancellationToken cancellationToken) =>
        ExecuteAsync(vehicleId, VehicleAction.Arm, MavLinkCommandIds.ComponentArmDisarm, [1], false, cancellationToken,
            async (response, token) => await eventHub.PublishDomainEventAsync(new VehicleArmed(response.VehicleId), token));

    /// <inheritdoc />
    public Task<VehicleCommandResponse> DisarmAsync(VehicleId vehicleId, CancellationToken cancellationToken) =>
        DisarmAsync(vehicleId, false, cancellationToken);

    /// <inheritdoc />
    public Task<VehicleCommandResponse> DisarmAsync(VehicleId vehicleId, bool safetyConfirmed, CancellationToken cancellationToken) =>
        ExecuteAsync(vehicleId, VehicleAction.Disarm, MavLinkCommandIds.ComponentArmDisarm, [0], safetyConfirmed, cancellationToken,
            async (response, token) => await eventHub.PublishDomainEventAsync(new VehicleDisarmed(response.VehicleId), token));

    /// <inheritdoc />
    public Task<VehicleCommandResponse> LandAsync(VehicleState state, CancellationToken cancellationToken) =>
        SetSemanticModeAsync(state.VehicleId, VehicleAction.Land, VehicleMode.Land, cancellationToken);

    /// <inheritdoc />
    public Task<VehicleCommandResponse> LandAsync(VehicleId vehicleId, CancellationToken cancellationToken) =>
        SetSemanticModeAsync(vehicleId, VehicleAction.Land, VehicleMode.Land, cancellationToken);

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
        if (state is null)
        {
            return Task.FromResult(NotFound(vehicleId));
        }

        if (!modeCatalog.GetModes(state.Identity.Firmware.Family).Contains(mode))
        {
            return Task.FromResult(Denied(vehicleId, $"{mode.Name} is not valid for {state.Identity.Firmware.Family}."));
        }

        return ExecuteModeAsync(vehicleId, VehicleAction.SetMode, mode, cancellationToken);
    }

    /// <inheritdoc />
    public Task<VehicleCommandResponse> TakeoffAsync(VehicleId vehicleId, double altitudeMeters, bool safetyConfirmed, CancellationToken cancellationToken)
    {
        if (!double.IsFinite(altitudeMeters) || altitudeMeters is < 1 or > 1000)
        {
            return Task.FromResult(Denied(vehicleId, "Takeoff altitude must be between 1 and 1000 metres."));
        }

        return ExecuteAsync(vehicleId, VehicleAction.Takeoff, MavLinkCommandIds.NavTakeoff,
            [0, 0, 0, 0, 0, 0, (float)altitudeMeters], safetyConfirmed, cancellationToken);
    }

    /// <inheritdoc />
    public Task<VehicleCommandResponse> ReturnToLaunchAsync(VehicleId vehicleId, CancellationToken cancellationToken) =>
        SetSemanticModeAsync(vehicleId, VehicleAction.ReturnToLaunch, VehicleMode.Rtl, cancellationToken);

    /// <inheritdoc />
    public Task<VehicleCommandResponse> HoldAsync(VehicleId vehicleId, CancellationToken cancellationToken) =>
        SetSemanticModeAsync(vehicleId, VehicleAction.Hold, VehicleMode.Loiter, cancellationToken);

    /// <inheritdoc />
    public Task<VehicleCommandResponse> RebootAutopilotAsync(VehicleId vehicleId, bool safetyConfirmed, CancellationToken cancellationToken) =>
        ExecuteAsync(vehicleId, VehicleAction.RebootAutopilot, MavLinkCommandIds.PreflightRebootShutdown,
            [1, 0, 0, 0, 0, 0, 0], safetyConfirmed, cancellationToken);

    /// <inheritdoc />
    public Task<VehicleCommandResponse> SetHomeHereAsync(VehicleId vehicleId, bool safetyConfirmed, CancellationToken cancellationToken) =>
        ExecuteAsync(vehicleId, VehicleAction.SetHomeHere, MavLinkCommandIds.DoSetHome,
            [1, 0, 0, 0, 0, 0, 0], safetyConfirmed, cancellationToken);

    /// <inheritdoc />
    public Task<VehicleCommandResponse> ExecuteExpertAsync(ExpertVehicleCommand command, bool safetyConfirmed, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.CommandId == 0 || command.Parameters.Count != 7 || command.Parameters.Any(value => !float.IsFinite(value)))
        {
            return Task.FromResult(Denied(command.VehicleId, "Expert command requires a non-zero ID and exactly seven finite parameters."));
        }

        if (command.CommandId is MavLinkCommandIds.ComponentArmDisarm or MavLinkCommandIds.DoSetMode or MavLinkCommandIds.PreflightRebootShutdown)
        {
            return Task.FromResult(Denied(command.VehicleId, "Use the typed safety-aware action for arm, mode, or reboot commands."));
        }

        return ExecuteAsync(command.VehicleId, VehicleAction.ExpertCommand, command.CommandId, command.Parameters, safetyConfirmed, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

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
        CancellationToken cancellationToken) =>
        ExecuteAsync(vehicleId, action, MavLinkCommandIds.DoSetMode, [1, mode.CustomMode], false, cancellationToken,
            async (response, token) =>
                await eventHub.PublishDomainEventAsync(
                    new VehicleModeChanged(new VehicleModeChange(response.VehicleId, mode.SemanticMode, clock.UtcNow)), token));

    private async Task<VehicleCommandResponse> ExecuteAsync(
        VehicleId vehicleId,
        VehicleAction action,
        ushort commandId,
        IReadOnlyList<float> parameters,
        bool safetyConfirmed,
        CancellationToken cancellationToken,
        Func<VehicleCommandResponse, CancellationToken, Task>? onAccepted = null)
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
            using var ackLifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var waitForAck = commandAckTracker.WaitForAckAsync(vehicleId, commandId, commandAckTimeout, ackLifetime.Token);

            try
            {
                await connection.SendRawAsync(packet, session.EndPoint, cancellationToken).ConfigureAwait(false);
                var ack = await waitForAck.ConfigureAwait(false);
                var response = new VehicleCommandResponse(vehicleId, MapResult(ack.Result), ack.ReceivedAt,
                    $"MAVLink ACK result {ack.Result}.");
                if (response.Result == VehicleCommandResult.Accepted && onAccepted is not null)
                {
                    await onAccepted(response, cancellationToken).ConfigureAwait(false);
                }

                return response;
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

    private VehicleCommandResponse NotFound(VehicleId vehicleId) =>
        new(vehicleId, VehicleCommandResult.VehicleNotFound, clock.UtcNow, "Vehicle is not registered.");

    private VehicleCommandResponse Denied(VehicleId vehicleId, string reason) =>
        new(vehicleId, VehicleCommandResult.Denied, clock.UtcNow, reason);

    private static VehicleCommandResult MapResult(byte mavResult) => mavResult switch
    {
        0 => VehicleCommandResult.Accepted,
        1 => VehicleCommandResult.TemporarilyRejected,
        2 => VehicleCommandResult.Denied,
        3 => VehicleCommandResult.Unsupported,
        4 => VehicleCommandResult.Failed,
        _ => VehicleCommandResult.Failed
    };
}
