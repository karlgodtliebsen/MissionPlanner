using Microsoft.Extensions.Logging;
using MissionPlanner.Core.DomainEvents;
using MissionPlanner.Core.Models;
using MissionPlanner.Core.Services;
using MissionPlanner.Core.Services.Abstractions;
using MissionPlanner.Core.VehicleHandler.Abstractions;
using MissionPlanner.Library;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.MavLink.Messages;

namespace MissionPlanner.Core.VehicleHandler;

/// <inheritdoc />
public sealed class AttitudeVehicleHandler(IVehicleRegistry vehicleRegistry, IDomainEventHub domainEventHub, ILogger<AttitudeVehicleHandler> logger) : IAttitudeVehicleHandler
{
    /// <inheritdoc />
    public async Task<VehicleSession> Handle(AttitudeMessage message, CancellationToken cancellationToken)
    {
        var vehicleId = new VehicleId(message.SystemId, message.ComponentId);

        logger.LogDebug("Handling attitude message from vehicle {VehicleId} {@Message}", vehicleId, message);
        var vehicle = vehicleRegistry.GetRequired(vehicleId);
        DomainException.ThrowIfNull(vehicle, nameof(vehicle));
        vehicle.ApplyAttitude(message.Roll, message.Pitch, message.Yaw);
        await domainEventHub.PublishDomainEventAsync(new VehicleStateUpdated(vehicle.State), cancellationToken);
        return vehicle;
    }
}
