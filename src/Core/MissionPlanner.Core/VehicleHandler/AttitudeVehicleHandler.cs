using Microsoft.Extensions.Logging;
using MissionPlanner.Core.DomainEvents;
using MissionPlanner.Core.Models;
using MissionPlanner.Core.Services.Abstractions;
using MissionPlanner.Core.VehicleHandler.Abstractions;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.MavLink.Messages;

namespace MissionPlanner.Core.VehicleHandler;

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

        logger.LogDebug("Handling attitude message from vehicle {VehicleId} {@Message}", vehicleId, message);
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
