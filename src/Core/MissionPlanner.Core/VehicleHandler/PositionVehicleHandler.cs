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

/// <summary>
/// Handles position messages and updates the vehicle registry accordingly.
/// </summary>
/// <param name="vehicleRegistry">The vehicle registry to update.</param>
/// <param name="domainEventHub"></param>
/// <param name="logger"></param>
public sealed class PositionVehicleHandler(IVehicleRegistry vehicleRegistry, IDomainEventHub domainEventHub, ILogger<PositionVehicleHandler> logger) : IPositionVehicleHandler
{
    /// <inheritdoc />
    public async Task<VehicleSession> Handle(GlobalPositionIntMessage message, CancellationToken cancellationToken)
    {
        var vehicleId = new VehicleId(message.SystemId, message.ComponentId);

        logger.LogDebug("Handling position message from vehicle {VehicleId} {@Message}", vehicleId, message);
        var vehicle = vehicleRegistry.GetRequired(vehicleId);
        DomainException.ThrowIfNull(vehicle, nameof(vehicle));

        vehicle.ApplyPosition(message.Latitude, message.Longitude, message.Altitude);
        await domainEventHub.PublishDomainEventAsync(new VehicleStateUpdated(vehicle.State), cancellationToken);
        return vehicle;
    }
}
