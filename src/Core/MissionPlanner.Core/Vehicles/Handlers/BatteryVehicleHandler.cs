using Microsoft.Extensions.Logging;
using MissionPlanner.Core.DomainEvents;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Handlers.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.MavLink.Messages;

namespace MissionPlanner.Core.Vehicles.Handlers;

/// <inheritdoc />
public sealed class BatteryVehicleHandler(IVehicleRegistry vehicleRegistry, IDomainEventHub domainEventHub, ILogger<BatteryVehicleHandler> logger) : IBatteryVehicleHandler
{
    /// <inheritdoc />
    public async Task Handle(SysStatusMessage message, CancellationToken cancellationToken)
    {
        var vehicleId = new VehicleId(message.SystemId, message.ComponentId);

        if (logger.IsEnabled(LogLevel.Trace))
        {
            logger.LogTrace("Handling battery status message from vehicle {VehicleId} {@Message}", vehicleId, message);
        }

        var vehicle = vehicleRegistry.GetRequired(vehicleId);
        if (vehicle is null)
        {
            logger.LogWarning("Vehicle {VehicleId} not found in registry. Cannot handle battery status message.", vehicleId);
            return;
        }

        vehicle.ApplyBattery(message.BatteryRemaining, message.BatteryVoltage);
        await domainEventHub.PublishDomainEventAsync(new VehicleStateUpdated(vehicle.State), cancellationToken);
    }
}
