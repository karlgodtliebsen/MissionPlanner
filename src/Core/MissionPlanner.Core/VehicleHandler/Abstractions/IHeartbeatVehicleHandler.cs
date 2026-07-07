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
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The updated vehicle session.</returns>
    Task<VehicleSession> Handle(HeartbeatMessage message, CancellationToken cancellationToken);
}
