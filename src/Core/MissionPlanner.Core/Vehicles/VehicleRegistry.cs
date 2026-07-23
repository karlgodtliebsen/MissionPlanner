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
    private readonly System.Collections.Concurrent.ConcurrentDictionary<VehicleId, VehicleSession> vehicles = [];

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
        foreach (var vehicleId in vehicles.Keys)
        {
            await RemoveAsync(vehicleId, cancellationToken).ConfigureAwait(false);
        }

        await eventHub.PublishDomainEventAsync(new VehicleRegistryReset(), cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> RemoveAsync(VehicleId vehicleId, CancellationToken cancellationToken = default)
    {
        if (!vehicles.TryRemove(vehicleId, out var vehicle))
        {
            return false;
        }

        var offlineState = vehicle.State with
        {
            Connection = vehicle.State.Connection with { State = VehicleConnectionState.Offline }
        };
        await eventHub.PublishDomainEventAsync(new VehicleStateUpdated(offlineState), cancellationToken).ConfigureAwait(false);
        logger.LogInformation("Removed exact vehicle {VehicleId} from the registry.", vehicleId);
        return true;
    }

    /// <inheritdoc />
    public async Task<VehicleUpdateConnectionStateResult> UpdateConnectionStates(DateTimeOffset now, TimeSpan staleAfter, TimeSpan degradedAfter, TimeSpan offlineAfter, CancellationToken cancellationToken)
    {
        var result = new List<VehicleSession>();
        foreach (var vehicle in vehicles.Values.ToArray())
        {
            result.Add(vehicle);
            VehicleConnectionStateChanged? stateChanged;
            lock (vehicle)
            {
                stateChanged = vehicle.UpdateConnectionState(now, staleAfter, degradedAfter, offlineAfter);
            }
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

            var candidate = new VehicleSession(state, endPoint, dateTimeProvider);
            identityWasNew = vehicles.TryAdd(vehicleId, candidate);
            session = identityWasNew ? candidate : vehicles[vehicleId];
            if (identityWasNew)
            {
                logger.LogTrace("Registered new vehicle: {VehicleId}", vehicleId);
                await eventHub.PublishDomainEventAsync(new VehicleRegistered(vehicleId), cancellationToken);
            }
        }

        string previousDisplayName;
        lock (session)
        {
            previousDisplayName = session.State.DisplayName;
            session.ApplyHeartbeat(
                customMode,
                vehicleType,
                autopilot,
                baseMode,
                systemStatus,
                mavLinkVersion,
                receivedAt);
        }

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
