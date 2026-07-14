using Microsoft.Extensions.Logging;
using MissionPlanner.Core.DomainEvents;
using MissionPlanner.Core.Models;
using MissionPlanner.Core.Services.Abstractions;
using MissionPlanner.Library.DateTime.Domain;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.Transport;

namespace MissionPlanner.Core.Services;

public sealed class VehicleRegistry(IDomainEventHub eventHub, IDateTimeProvider dateTimeProvider, ILogger<VehicleRegistry> logger) : IVehicleRegistry
{
    private readonly Dictionary<VehicleId, VehicleSession> vehicles = [];

    public VehicleSession? GetRequired(VehicleId vehicleId)
    {
        vehicles.TryGetValue(vehicleId, out var vehicle);
        if (vehicle is null)
        {
            logger.LogWarning("Vehicle '{VehicleId}' is not registered.", vehicleId);
        }

        return vehicle;
    }

    public IReadOnlyCollection<VehicleSession> Vehicles => vehicles.Values.ToArray();

    public void Reset()
    {
        foreach (var vehicle in vehicles.Values)
        {
            var offlineState = vehicle.State with { Connection = vehicle.State.Connection with { State = VehicleConnectionState.Offline } };

            eventHub.PublishDomainEvent(new VehicleStateUpdated(offlineState));
        }

        vehicles.Clear();
        eventHub.PublishDomainEvent(new VehicleRegistryReset());
    }

    public VehicleUpdateConnectionStateResult UpdateConnectionStates(
        DateTimeOffset now,
        TimeSpan staleAfter,
        TimeSpan degradedAfter,
        TimeSpan offlineAfter)
    {
        var result = new List<VehicleSession>();
        foreach (var vehicle in vehicles.Values)
        {
            result.Add(vehicle);
            var stateChanged = vehicle.UpdateConnectionState(now, staleAfter, degradedAfter, offlineAfter);
            eventHub.PublishDomainEvent(new VehicleStateUpdated(vehicle.State));
            if (stateChanged is not null)
            {
                eventHub.PublishDomainEvent(stateChanged);
            }
        }

        logger.LogTrace("Updated connection states for {VehicleCount} vehicles.", vehicles.Count);
        return new VehicleUpdateConnectionStateResult(result);
    }

    public VehicleRegistryResult RegisterOrUpdateHeartbeat(
        VehicleId vehicleId,
        TransportEndPoint endPoint,
        uint customMode,
        byte vehicleType,
        byte autopilot,
        byte baseMode,
        byte systemStatus,
        byte mavLinkVersion,
        DateTimeOffset receivedAt)
    {
        if (!vehicles.TryGetValue(vehicleId, out var session))
        {
            var state = new VehicleState(
                vehicleId,
                new VehicleIdentityState(vehicleType, autopilot, mavLinkVersion),
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
            eventHub.PublishDomainEvent(new VehicleRegistered(vehicleId));
        }

        session.ApplyHeartbeat(
            customMode,
            vehicleType,
            autopilot,
            baseMode,
            systemStatus,
            mavLinkVersion,
            receivedAt);

        return new VehicleRegistryResult(session);
    }
}
