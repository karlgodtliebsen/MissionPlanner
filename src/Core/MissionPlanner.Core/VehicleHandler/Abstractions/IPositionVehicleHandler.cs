using MissionPlanner.Core.Services;
using MissionPlanner.MavLink.Messages;

namespace MissionPlanner.Core.VehicleHandler.Abstractions;

/// <summary>
/// Defines the interface for handling position messages and updating the vehicle registry accordingly.
/// </summary>
public interface IPositionVehicleHandler
{
    /// <summary>
    /// Handles a position message and updates the vehicle registry accordingly.
    /// </summary>
    /// <param name="message">The position message to handle.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The updated vehicle session.</returns>
    Task<VehicleSession> Handle(GlobalPositionIntMessage message, CancellationToken cancellationToken);
}
