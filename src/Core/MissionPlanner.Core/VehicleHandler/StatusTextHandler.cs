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
/// Handles status text messages from vehicles.
/// </summary>
/// <param name="vehicleRegistry">The vehicle registry to update.</param>
/// <param name="domainEventHub"></param>
/// <param name="logger">The logger to use for logging.</param>
public sealed class StatusTextHandler(IVehicleRegistry vehicleRegistry, IDomainEventHub domainEventHub, ILogger<StatusTextHandler> logger) : IStatusTextHandler
{
    /// <inheritdoc />
    public async Task<VehicleSession> Handle(StatusTextMessage message, CancellationToken cancellationToken)
    {
        var vehicleId = new VehicleId(message.SystemId, message.ComponentId);
        logger.LogDebug("Handling status text message from vehicle {VehicleId} {@Message}", vehicleId, message);
        var vehicle = vehicleRegistry.GetRequired(vehicleId);
        DomainException.ThrowIfNull(vehicle, nameof(vehicle));
        vehicle.ApplyStatusText(message);
        await domainEventHub.PublishDomainEventAsync(new VehicleStateUpdated(vehicle.State), cancellationToken);
        return vehicle;
    }
}
