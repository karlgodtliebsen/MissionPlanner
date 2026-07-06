using System.Net;
using MissionPlanner.Core.Services;
using MissionPlanner.Transport;

namespace MissionPlanner.Simulator;

/// <summary>
/// 
/// </summary>
public static class SimulatedVehicleStateExtensions
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="simulated"></param>
    /// <param name="registry"></param>
    /// <returns></returns>
    public static VehicleSession ApplyTo(this SimulatedVehicleState simulated, IVehicleRegistry registry)
    {
        var vehicleRegistryResult = registry.RegisterOrUpdateHeartbeat(
            simulated.VehicleId,
            new IPEndPoint(IPAddress.Any, 0).ToTransportEndPoint("udp"),
            simulated.CustomMode,
            simulated.VehicleType,
            simulated.Autopilot,
            simulated.BaseMode,
            simulated.SystemStatus,
            simulated.MavLinkVersion,
            simulated.Timestamp);

        if (simulated.Latitude is not null &&
            simulated.Longitude is not null &&
            simulated.Altitude is not null)
        {
            vehicleRegistryResult.Vehicle.ApplyPosition(simulated.Latitude.Value, simulated.Longitude.Value, simulated.Altitude.Value);
        }

        if (simulated.Roll is not null &&
            simulated.Pitch is not null &&
            simulated.Yaw is not null)
        {
            vehicleRegistryResult.Vehicle.ApplyAttitude(simulated.Roll.Value, simulated.Pitch.Value, simulated.Yaw.Value);
        }

        vehicleRegistryResult.Vehicle.ApplyBattery(simulated.BatteryRemaining, simulated.BatteryVoltage);

        return vehicleRegistryResult.Vehicle;
    }
}
