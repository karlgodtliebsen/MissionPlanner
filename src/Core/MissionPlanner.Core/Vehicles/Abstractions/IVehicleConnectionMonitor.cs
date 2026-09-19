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

    /// <summary>Tracks one owned connection; disposing the lease stops monitoring without reconnecting.</summary>
    IDisposable? Track(MissionPlanner.Shared.Models.Vehicles.Models.VehicleId vehicleId, Guid connectionId,
        IVehicleConnectionSession session, Func<string, Task> disconnect);
}
