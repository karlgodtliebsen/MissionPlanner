using MissionPlanner.MavLink.Messages;

namespace MissionPlanner.Core.Vehicles.Handlers.Abstractions;

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
    Task Handle(HeartbeatMessage message, CancellationToken cancellationToken);
}
