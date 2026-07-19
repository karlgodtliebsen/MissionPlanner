namespace MissionPlanner.Core.Vehicles.Abstractions;

/// <summary>
/// Monitors the connection state of vehicles.
/// </summary>
public interface IVehicleConnectionMonitor
{
    /// <summary>
    /// Updates the connection states of all monitored vehicles.
    /// </summary>
    Task UpdateConnectionStatesAsync(CancellationToken cancellationToken);
}
