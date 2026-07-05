using Microsoft.Extensions.Logging;
using MissionPlanner.Core.Models;
using MissionPlanner.Core.Services;
using MissionPlanner.MavLink.Messages;

namespace MissionPlanner.Core.VehicleHandler;

/// <inheritdoc />
public sealed class BatteryVehicleHandler(IVehicleRegistry vehicleRegistry, ILogger<BatteryVehicleHandler> logger) : IBatteryVehicleHandler
{
    /// <inheritdoc />
    public void Handle(SysStatusMessage message)
    {
        var vehicleId = new VehicleId(message.SystemId, message.ComponentId);

        logger.LogTrace("Handling battery status message from vehicle {VehicleId}", vehicleId);
        var vehicle = vehicleRegistry.GetRequired(vehicleId);

        vehicle?.ApplyBattery(message.BatteryRemaining, message.BatteryVoltage);
    }
}