using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Library.DateTime.Domain;

namespace MissionPlanner.Core.Vehicles;

/// <summary>
/// Monitors the connection state of vehicles.
/// </summary>
/// <param name="vehicleRegistry">The vehicle registry to monitor.</param>
/// <param name="clock">The clock to use for time-based calculations.</param>
public sealed class VehicleConnectionMonitor(IVehicleRegistry vehicleRegistry, IDateTimeProvider clock) : IVehicleConnectionMonitor
{
    private static readonly TimeSpan StaleAfter = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan DegradedAfter = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan OfflineAfter = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Updates the connection states of all monitored vehicles.
    /// </summary>
    public async Task UpdateConnectionStatesAsync(CancellationToken cancellationToken)
    {
        await vehicleRegistry.UpdateConnectionStates(clock.UtcNow, StaleAfter, DegradedAfter, OfflineAfter, cancellationToken);
    }
}
