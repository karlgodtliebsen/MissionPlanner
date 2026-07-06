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
    /// <returns>The updated vehicle session.</returns>
    void Handle(AttitudeMessage message);
}
