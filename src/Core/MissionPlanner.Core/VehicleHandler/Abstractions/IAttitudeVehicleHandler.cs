using MissionPlanner.Core.Services;
using MissionPlanner.MavLink.Messages;

namespace MissionPlanner.Core.VehicleHandler.Abstractions;

/// <summary>
/// Defines the interface for handling attitude messages and updating the vehicle registry accordingly.
/// </summary>
public interface IAttitudeVehicleHandler
{
    /// <summary>
    /// Handles an attitude message and updates the vehicle registry accordingly.
    /// </summary>
    /// <param name="message">The attitude message to handle.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The updated vehicle session.</returns>
    Task<VehicleSession> Handle(AttitudeMessage message, CancellationToken cancellationToken);
}
