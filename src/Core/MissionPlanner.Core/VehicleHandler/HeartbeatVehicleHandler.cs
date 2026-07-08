using Microsoft.Extensions.Logging;
using MissionPlanner.Core.DomainEvents;
using MissionPlanner.Core.Models;
using MissionPlanner.Core.Services.Abstractions;
using MissionPlanner.Core.VehicleHandler.Abstractions;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.MavLink.Messages;

namespace MissionPlanner.Core.VehicleHandler;

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
            logger.LogTrace("HeartbeatVehicleHandler - Handling heartbeat message from vehicle {VehicleId} {@Message}", new VehicleId(message.SystemId, message.ComponentId), message);
        }

        var vehicleId = new VehicleId(message.SystemId, message.ComponentId);

        var registryResult = vehicleRegistry.RegisterOrUpdateHeartbeat(
            vehicleId,
            message.EndPoint,
            message.CustomMode,
            message.VehicleType,
            message.Autopilot,
            message.BaseMode,
            message.SystemStatus,
            message.MavLinkVersion,
            message.ReceivedAt);
        await domainEventHub.PublishDomainEventAsync(new VehicleStateUpdated(registryResult.Vehicle.State), cancellationToken);
    }
}
