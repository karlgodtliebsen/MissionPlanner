using MissionPlanner.MavLink.Messages;

namespace MissionPlanner.Core.Vehicles.Handlers.Abstractions;

/// <summary>
/// Defines the interface for handling battery status messages and updating the vehicle registry accordingly.
/// </summary>
public interface IBatteryVehicleHandler
{
    /// <summary>
    /// Handles a battery status message and updates the vehicle registry accordingly.
    /// </summary>
    /// <param name="message">The battery status message to handle.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>   
    /// <returns>The updated vehicle session.</returns>
    Task Handle(SysStatusMessage message, CancellationToken cancellationToken);
}
