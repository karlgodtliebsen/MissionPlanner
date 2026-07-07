using Microsoft.Extensions.Logging;
using MissionPlanner.Core.DomainEvents;
using MissionPlanner.Core.Models;
using MissionPlanner.Core.Services.Abstractions;
using MissionPlanner.Core.VehicleHandler.Abstractions;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.MavLink.Messages;

namespace MissionPlanner.Core.VehicleHandler;

/// <inheritdoc />
public sealed class BatteryVehicleHandler(IVehicleRegistry vehicleRegistry, IDomainEventHub domainEventHub, ILogger<BatteryVehicleHandler> logger) : IBatteryVehicleHandler
{
    /// <inheritdoc />
    public async Task Handle(SysStatusMessage message, CancellationToken cancellationToken)
    {
        var vehicleId = new VehicleId(message.SystemId, message.ComponentId);

        logger.LogDebug("Handling battery status message from vehicle {VehicleId} {@Message}", vehicleId, message);
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
