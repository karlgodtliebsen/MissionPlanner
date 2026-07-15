using Microsoft.Extensions.Logging;
using MissionPlanner.Core.DomainEvents;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Handlers.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.MavLink.Messages;

namespace MissionPlanner.Core.Vehicles.Handlers;

/// <inheritdoc />
public sealed class AttitudeVehicleHandler(
    IVehicleRegistry vehicleRegistry,
    IDomainEventHub domainEventHub,
    ILogger<AttitudeVehicleHandler> logger) : IAttitudeVehicleHandler
{
    /// <inheritdoc />
    public async Task Handle(AttitudeMessage message, CancellationToken cancellationToken)
    {
        var vehicleId = new VehicleId(message.SystemId, message.ComponentId);

        if (logger.IsEnabled(LogLevel.Trace))
        {
            logger.LogTrace("Handling attitude message from vehicle {VehicleId} {@Message}", vehicleId, message);
        }

        var vehicle = vehicleRegistry.GetRequired(vehicleId);
        if (vehicle is null)
        {
            logger.LogWarning("Vehicle {VehicleId} not found in registry. Cannot handle attitude message.", vehicleId);
            return;
        }

        vehicle.ApplyAttitude(message.Roll, message.Pitch, message.Yaw);
        await domainEventHub.PublishDomainEventAsync(new VehicleStateUpdated(vehicle.State), cancellationToken);
    }
}
