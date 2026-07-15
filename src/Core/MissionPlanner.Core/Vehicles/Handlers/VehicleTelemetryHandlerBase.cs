using MissionPlanner.Core.DomainEvents;
using MissionPlanner.Core.Models;
using MissionPlanner.Core.Services;
using MissionPlanner.Core.Services.Abstractions;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.MavLink.Messages;

namespace MissionPlanner.Core.VehicleHandler;

public abstract class VehicleTelemetryHandlerBase(
    IVehicleRegistry vehicleRegistry,
    IDomainEventHub domainEventHub)
{
    protected VehicleSession? GetVehicle(MavLinkMessage message) =>
        vehicleRegistry.GetRequired(new VehicleId(message.SystemId, message.ComponentId));

    protected ValueTask PublishStateAsync(VehicleSession vehicle, CancellationToken cancellationToken) =>
        new(domainEventHub.PublishDomainEventAsync(new VehicleStateUpdated(vehicle.State), cancellationToken));
}
