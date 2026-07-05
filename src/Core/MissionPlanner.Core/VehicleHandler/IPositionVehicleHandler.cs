using MissionPlanner.MavLink.Messages;

namespace MissionPlanner.Core.VehicleHandler;

/// <summary>
/// Defines the interface for handling position messages and updating the vehicle registry accordingly.
/// </summary>
public interface IPositionVehicleHandler
{
    /// <summary>
    /// Handles a position message and updates the vehicle registry accordingly.
    /// </summary>
    /// <param name="message">The position message to handle.</param>
    void Handle(GlobalPositionIntMessage message);
}