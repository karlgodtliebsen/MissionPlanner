using MissionPlanner.Core.Services;
using MissionPlanner.MavLink.Messages;

namespace MissionPlanner.Core.VehicleHandler.Abstractions;

/// <summary>
/// Handles heartbeat messages and updates the vehicle registry accordingly.
/// </summary>
public interface IHeartbeatVehicleHandler
{
    /// <summary>
    /// Handles a heartbeat message and updates the vehicle registry accordingly.
    /// </summary>
    /// <param name="message">The heartbeat message to handle.</param>
    /// <returns>The updated vehicle session.</returns>
    VehicleSession Handle(HeartbeatMessage message);
}
