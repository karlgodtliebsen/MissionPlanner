using MissionPlanner.Core.Services;
using MissionPlanner.MavLink.Messages;

namespace MissionPlanner.Core.VehicleHandler.Abstractions;

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
    Task<VehicleSession> Handle(SysStatusMessage message, CancellationToken cancellationToken);
}
