using Microsoft.Extensions.Logging;
using MissionPlanner.Core.Models;
using MissionPlanner.Core.Services;
using MissionPlanner.MavLink.Messages;

namespace MissionPlanner.Core.VehicleHandler;

/// <summary>
/// Handles position messages and updates the vehicle registry accordingly.
/// </summary>
/// <param name="vehicleRegistry">The vehicle registry to update.</param>
/// <param name="logger"></param>
public sealed class PositionVehicleHandler(IVehicleRegistry vehicleRegistry, ILogger<PositionVehicleHandler> logger) : IPositionVehicleHandler
{
    /// <inheritdoc/>
    public void Handle(GlobalPositionIntMessage message)
    {
        var vehicleId = new VehicleId(message.SystemId, message.ComponentId);

        logger.LogTrace("Handling position message from vehicle {VehicleId}", vehicleId);
        var vehicle = vehicleRegistry.GetRequired(vehicleId);

        vehicle?.ApplyPosition(message.Latitude, message.Longitude, message.Altitude);
    }
}