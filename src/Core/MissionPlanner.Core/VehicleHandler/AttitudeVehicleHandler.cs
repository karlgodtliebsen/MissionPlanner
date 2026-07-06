using Microsoft.Extensions.Logging;
using MissionPlanner.Core.Models;
using MissionPlanner.Core.Services;
using MissionPlanner.Core.Services.Abstractions;
using MissionPlanner.Core.VehicleHandler.Abstractions;
using MissionPlanner.MavLink.Messages;

namespace MissionPlanner.Core.VehicleHandler;

/// <inheritdoc />
public sealed class AttitudeVehicleHandler(IVehicleRegistry vehicleRegistry, ILogger<AttitudeVehicleHandler> logger) : IAttitudeVehicleHandler
{
    /// <inheritdoc />
    public void Handle(AttitudeMessage message)
    {
        var vehicleId = new VehicleId(message.SystemId, message.ComponentId);

        logger.LogTrace("Handling attitude message from vehicle {VehicleId}", vehicleId);
        var vehicle = vehicleRegistry.GetRequired(vehicleId);

        vehicle?.ApplyAttitude(
            message.Roll,
            message.Pitch,
            message.Yaw);
    }
}
