using MissionPlanner.MavLink.Messages;

namespace MissionPlanner.Core.VehicleHandler.Abstractions;

/// <summary>
/// Defines the interface for handling status text messages and updating the vehicle registry accordingly.
/// </summary>
public interface IStatusTextHandler
{
    /// <summary>
    /// Handles a status text message and updates the vehicle registry accordingly.
    /// </summary>
    /// <param name="message">The status text message to handle.</param>
    /// <returns>The updated vehicle session.</returns>
    void Handle(StatusTextMessage message);
}
