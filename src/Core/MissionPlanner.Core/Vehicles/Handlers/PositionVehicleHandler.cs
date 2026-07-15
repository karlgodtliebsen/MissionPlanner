using Microsoft.Extensions.Logging;
using MissionPlanner.Core.DomainEvents;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Handlers.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.MavLink.Messages;

namespace MissionPlanner.Core.Vehicles.Handlers;

/// <summary>
/// Handles position messages and updates the vehicle registry accordingly.
/// </summary>
/// <param name="vehicleRegistry">The vehicle registry to update.</param>
/// <param name="domainEventHub"></param>
/// <param name="logger"></param>
public sealed class PositionVehicleHandler(IVehicleRegistry vehicleRegistry, IDomainEventHub domainEventHub, ILogger<PositionVehicleHandler> logger) : IPositionVehicleHandler
{
    /// <inheritdoc />
    public async Task Handle(GlobalPositionIntMessage message, CancellationToken cancellationToken)
    {
        var vehicleId = new VehicleId(message.SystemId, message.ComponentId);
        if (logger.IsEnabled(LogLevel.Trace))
        {
            logger.LogTrace("Handling position message from vehicle {VehicleId} {@Message}", vehicleId, message);
        }

        var vehicle = vehicleRegistry.GetRequired(vehicleId);
        if (vehicle is null)
        {
            logger.LogWarning("Vehicle {VehicleId} not found in registry. Cannot handle position message.", vehicleId);
            return;
        }

        vehicle.ApplyPosition(message.LatitudeDegrees, message.LongitudeDegrees, message.AltitudeMslMeters);
        await domainEventHub.PublishDomainEventAsync(new VehicleStateUpdated(vehicle.State), cancellationToken);
    }
}
