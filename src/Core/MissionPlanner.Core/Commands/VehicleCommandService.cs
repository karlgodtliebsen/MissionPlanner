using Microsoft.Extensions.Logging;
using MissionPlanner.Core.DomainEvents;
using MissionPlanner.Core.Services;
using MissionPlanner.Core.Services.Abstractions;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Library.DateTime.Domain;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.MavLink.Commands;
using MissionPlanner.MavLink.Encoding;
using MissionPlanner.MavLink.Services.Abstractions;

namespace MissionPlanner.Core.Commands;

/// <summary>
/// Service for sending commands to vehicles.
/// </summary>
public sealed class VehicleCommandService(
    IVehicleRegistry registry,
    IDomainEventHub eventHub,
    IMavLinkConnection connection,
    IMavLinkCommandEncoder encoder,
    ICommandAckTracker commandAckTracker,
    IDateTimeProvider dateTimeProvider,
    IVehicleCommandPolicy commandPolicy,
    ILogger<VehicleCommandService> logger)
    : IVehicleCommandService
{
    private static readonly TimeSpan CommandAckTimeout = TimeSpan.FromSeconds(5);

    private VehicleCommandResponse? ValidateLand(VehicleState state)
    {
        return state.ConnectionState != VehicleConnectionState.Online
            ? new VehicleCommandResponse(state.VehicleId, VehicleCommandResult.Denied, dateTimeProvider.UtcNow)
            : !state.IsArmed
                ? new VehicleCommandResponse(state.VehicleId, VehicleCommandResult.Denied, dateTimeProvider.UtcNow)
                : null;
    }


    private VehicleCommandResponse? ValidateCanSetMode(VehicleId vehicleId, VehicleMode mode)
    {
        var vehicle = registry.GetRequired(vehicleId);
        if (vehicle is null)
        {
            return new VehicleCommandResponse(vehicleId, VehicleCommandResult.VehicleNotFound, dateTimeProvider.UtcNow);
        }

        var validation = commandPolicy.ValidateSetMode(vehicle.State, mode);
        return validation;
    }

    private VehicleCommandResponse? ValidateCanCommand(VehicleId vehicleId)
    {
        var vehicle = registry.GetRequired(vehicleId);
        if (vehicle is null)
        {
            return new VehicleCommandResponse(vehicleId, VehicleCommandResult.VehicleNotFound, dateTimeProvider.UtcNow);
        }

        var validation = commandPolicy.ValidateArm(vehicle.State);
        return validation;
    }

    /// <inheritdoc />
    public Task<VehicleCommandResponse> LandAsync(VehicleState state, CancellationToken cancellationToken)
    {
        var validation = ValidateLand(state);
        if (validation is not null)
        {
            return Task.FromResult(validation);
        }

        var vehicleId = state.VehicleId;
        return SetModeAsync(vehicleId, VehicleMode.Land, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<VehicleCommandResponse> ArmAsync(VehicleId vehicleId, CancellationToken cancellationToken)
    {
        var validation = ValidateCanCommand(vehicleId);
        if (validation is not null)
        {
            return validation;
        }

        var result = await SendArmDisarmAsync(vehicleId, true, cancellationToken);

        if (result.Result == VehicleCommandResult.Accepted)
        {
            await eventHub.PublishDomainEventAsync(new VehicleArmed(vehicleId), cancellationToken);
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<VehicleCommandResponse> DisarmAsync(VehicleId vehicleId, CancellationToken cancellationToken)
    {
        var validation = ValidateCanCommand(vehicleId);
        if (validation is not null)
        {
            return validation;
        }

        var result = await SendArmDisarmAsync(vehicleId, false, cancellationToken);
        if (result.Result == VehicleCommandResult.Accepted)
        {
            await eventHub.PublishDomainEventAsync(new VehicleDisarmed(vehicleId), cancellationToken);
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<VehicleCommandResponse> SetModeAsync(VehicleId vehicleId, VehicleMode mode, CancellationToken cancellationToken)
    {
        var validation = ValidateCanCommand(vehicleId);

        if (validation is not null)
        {
            return validation;
        }

        validation = ValidateCanSetMode(vehicleId, mode);

        if (validation is not null)
        {
            return validation;
        }

        var vehicleSession = registry.GetRequired(vehicleId)!;

        var customMode = ArduCopterModeMapper.ToCustomMode(mode);

        var waitForAckTask = commandAckTracker.WaitForAckAsync(vehicleId, MavLinkCommandIds.DoSetMode, CommandAckTimeout, cancellationToken);

        var packet = encoder.EncodeSetMode(vehicleId.SystemId, vehicleId.ComponentId, customMode);

        await connection.SendRawAsync(packet, vehicleSession.EndPoint, cancellationToken);

        try
        {
            var ack = await waitForAckTask.ConfigureAwait(false);
            await eventHub.PublishDomainEventAsync(new VehicleModeChanged(new VehicleModeChange(vehicleId, mode, dateTimeProvider.UtcNow)), cancellationToken);
            return new VehicleCommandResponse(vehicleId, MapResult(ack.Result), ack.ReceivedAt);
        }
        catch (TimeoutException)
        {
            return new VehicleCommandResponse(vehicleId, VehicleCommandResult.Timeout, dateTimeProvider.UtcNow);
        }
    }


    private async Task<VehicleCommandResponse> SendArmDisarmAsync(VehicleId vehicleId, bool arm, CancellationToken cancellationToken)
    {
        var validation = ValidateCanCommand(vehicleId);

        if (validation is not null)
        {
            return validation;
        }

        var vehicleSession = registry.GetRequired(vehicleId)!;

        var waitForAckTask = commandAckTracker.WaitForAckAsync(vehicleId, MavLinkCommandIds.ComponentArmDisarm, CommandAckTimeout, cancellationToken);

        var packet = encoder.EncodeArmDisarm(vehicleId.SystemId, vehicleId.ComponentId, arm);

        await connection.SendRawAsync(packet, vehicleSession.EndPoint, cancellationToken).ConfigureAwait(false);

        try
        {
            var ack = await waitForAckTask.ConfigureAwait(false);

            return new VehicleCommandResponse(vehicleId, MapResult(ack.Result), ack.ReceivedAt);
        }
        catch (TimeoutException)
        {
            return new VehicleCommandResponse(vehicleId, VehicleCommandResult.Timeout, dateTimeProvider.UtcNow);
        }
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

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await connection.DisposeAsync().ConfigureAwait(false);
    }
}
