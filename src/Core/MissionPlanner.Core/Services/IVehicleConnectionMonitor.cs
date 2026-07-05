namespace MissionPlanner.Core.Services;

/// <summary>
/// Monitors the connection state of vehicles.
/// </summary>
public interface IVehicleConnectionMonitor
{
    /// <summary>
    /// Updates the connection states of all monitored vehicles.
    /// </summary>
    void UpdateConnectionStates();
}