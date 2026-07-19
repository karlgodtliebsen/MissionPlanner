using Microsoft.Extensions.Logging;
using MissionPlanner.Core.DomainEvents;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Handlers.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.MavLink.Messages;

namespace MissionPlanner.Core.Vehicles.Handlers;

/// <summary>
/// Handles heartbeat messages and updates the vehicle registry accordingly.
/// </summary>
public sealed class HeartbeatVehicleHandler(IVehicleRegistry vehicleRegistry, IDomainEventHub domainEventHub, ILogger<HeartbeatVehicleHandler> logger) : IHeartbeatVehicleHandler
{
    /// <inheritdoc />
    public async Task Handle(HeartbeatMessage message, CancellationToken cancellationToken)
    {
        if (logger.IsEnabled(LogLevel.Trace))
        {
            logger.LogTrace("HeartbeatVehicleHandler - Handling heartbeat message from vehicle {VehicleId} {MessageId}", new VehicleId(message.SystemId, message.ComponentId), message.MessageId);
        }

        var vehicleId = new VehicleId(message.SystemId, message.ComponentId);

        var registryResult = await vehicleRegistry.RegisterOrUpdateHeartbeatAsync(
            vehicleId,
            message.EndPoint,
            message.CustomMode,
            message.VehicleType,
            message.Autopilot,
            message.BaseMode,
            message.SystemStatus,
            message.MavLinkVersion,
            message.ReceivedAt, cancellationToken);
        await domainEventHub.PublishDomainEventAsync(new VehicleStateUpdated(registryResult.Vehicle.State), cancellationToken);
    }
}
