using Microsoft.Extensions.Logging;
using MissionPlanner.Core.DomainEvents;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Library.DateTime.Domain;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.Transport;

namespace MissionPlanner.Core.Vehicles;

/// <inheritdoc />
public sealed class VehicleRegistry(IDomainEventHub eventHub, IDateTimeProvider dateTimeProvider, ILogger<VehicleRegistry> logger) : IVehicleRegistry
{
    private readonly Dictionary<VehicleId, VehicleSession> vehicles = [];

    /// <inheritdoc />
    public VehicleSession? GetRequired(VehicleId vehicleId)
    {
        vehicles.TryGetValue(vehicleId, out var vehicle);
        if (vehicle is null)
        {
            logger.LogWarning("Vehicle '{VehicleId}' is not registered.", vehicleId);
        }

        return vehicle;
    }

    /// <inheritdoc />
    public IReadOnlyCollection<VehicleSession> Vehicles => vehicles.Values.ToArray();

    /// <summary>
    /// Resets the vehicle registry by clearing all registered vehicles and publishing a VehicleRegistryReset event.
    /// </summary>
    public async Task Reset(CancellationToken cancellationToken)
    {
        foreach (var vehicle in vehicles.Values)
        {
            var offlineState = vehicle.State with { Connection = vehicle.State.Connection with { State = VehicleConnectionState.Offline } };

            await eventHub.PublishDomainEventAsync(new VehicleStateUpdated(offlineState), cancellationToken);
        }

        vehicles.Clear();
        await eventHub.PublishDomainEventAsync(new VehicleRegistryReset(), cancellationToken);
    }

    /// <inheritdoc />
    public async Task<VehicleUpdateConnectionStateResult> UpdateConnectionStates(DateTimeOffset now, TimeSpan staleAfter, TimeSpan degradedAfter, TimeSpan offlineAfter, CancellationToken cancellationToken)
    {
        var result = new List<VehicleSession>();
        foreach (var vehicle in vehicles.Values)
        {
            result.Add(vehicle);
            var stateChanged = vehicle.UpdateConnectionState(now, staleAfter, degradedAfter, offlineAfter);
            await eventHub.PublishDomainEventAsync(new VehicleStateUpdated(vehicle.State), cancellationToken);
            if (stateChanged is not null)
            {
                await eventHub.PublishDomainEventAsync(stateChanged, cancellationToken);
            }
        }

        logger.LogTrace("Updated connection states for {VehicleCount} vehicles.", vehicles.Count);
        return new VehicleUpdateConnectionStateResult(result);
    }

    /// <summary>
    /// Registers a new vehicle or updates the heartbeat of an existing vehicle.
    /// </summary>
    /// <param name="vehicleId">The unique identifier of the vehicle.</param>
    /// <param name="endPoint">The transport endpoint of the vehicle.</param>
    /// <param name="customMode">The custom mode of the vehicle.</param>
    /// <param name="vehicleType">The type of the vehicle.</param>
    /// <param name="autopilot">The autopilot type of the vehicle.</param>
    /// <param name="baseMode">The base mode of the vehicle.</param>
    /// <param name="systemStatus">The system status of the vehicle.</param>
    /// <param name="mavLinkVersion">The MAVLink version of the vehicle.</param>
    /// <param name="receivedAt">The timestamp when the heartbeat was received.</param>
    /// <param name="cancellationToken"></param>
    /// <returns>A result containing the updated vehicle session.</returns>
    public async Task<VehicleRegistryResult> RegisterOrUpdateHeartbeatAsync(
        VehicleId vehicleId,
        TransportEndPoint endPoint,
        uint customMode,
        byte vehicleType,
        byte autopilot,
        byte baseMode,
        byte systemStatus,
        byte mavLinkVersion,
        DateTimeOffset receivedAt,
        CancellationToken cancellationToken)
    {
        var identityWasNew = false;
        if (!vehicles.TryGetValue(vehicleId, out var session))
        {
            identityWasNew = true;
            var state = new VehicleState(
                vehicleId,
                new VehicleIdentityState(vehicleType, autopilot, mavLinkVersion, VehicleFirmwareIdentityFactory.FromHeartbeat(vehicleType, autopilot)),
                new VehicleConnectionData(VehicleConnectionState.Unknown, receivedAt),
                new VehicleFlightState(customMode, baseMode, systemStatus, VehicleMode.Unknown, false),
                VehiclePositionState.Empty,
                VehicleMotionState.Empty,
                VehicleGpsState.Empty,
                VehiclePowerState.Empty,
                VehicleRadioState.Empty,
                VehicleNavigationState.Empty,
                VehicleHealthState.Empty);

            session = new VehicleSession(state, endPoint, dateTimeProvider);
            vehicles.Add(vehicleId, session);
            logger.LogTrace("Registered new vehicle: {VehicleId}", vehicleId);
            await eventHub.PublishDomainEventAsync(new VehicleRegistered(vehicleId), cancellationToken);
        }

        var previousDisplayName = session.State.DisplayName;
        session.ApplyHeartbeat(
            customMode,
            vehicleType,
            autopilot,
            baseMode,
            systemStatus,
            mavLinkVersion,
            receivedAt);

        if (identityWasNew || !string.Equals(previousDisplayName, session.State.DisplayName, StringComparison.Ordinal))
        {
            logger.LogDebug(
                "Resolved vehicle identity: VehicleId={VehicleId}, Autopilot={Autopilot}, MavType={MavType}, FirmwareFamily={FirmwareFamily}, DisplayName={DisplayName}",
                vehicleId,
                autopilot,
                vehicleType,
                session.State.Identity.Firmware.Family,
                session.State.DisplayName);
        }

        return new VehicleRegistryResult(session);
    }
}
