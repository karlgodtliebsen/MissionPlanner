using MissionPlanner.Core.DomainEvents;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.MavLink.Messages;

namespace MissionPlanner.Core.Vehicles.Handlers;

public abstract class VehicleTelemetryHandlerBase(
    IVehicleRegistry vehicleRegistry,
    IDomainEventHub domainEventHub)
{
    protected VehicleSession? GetVehicle(MavLinkMessage message)
    {
        return vehicleRegistry.GetRequired(new VehicleId(message.SystemId, message.ComponentId));
    }

    protected ValueTask PublishStateAsync(VehicleSession vehicle, CancellationToken cancellationToken)
    {
        return new ValueTask(domainEventHub.PublishDomainEventAsync(new VehicleStateUpdated(vehicle.State), cancellationToken));
    }
}
