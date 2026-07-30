using MissionPlanner.Core.Vehicles.Models;

namespace MissionPlanner.Core.Simulation;

/// <summary>Connects an owned simulator endpoint through the existing vehicle connection stack.</summary>
public interface ISimulatorVehicleConnection
{
    /// <summary>Connects and verifies the expected heartbeat identity.</summary>
    /// <param name="profile">Expected simulator profile.</param>
    /// <param name="endpoint">MAVLink listening endpoint.</param>
    /// <param name="timeout">Maximum connection/heartbeat wait.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The exact connected vehicle identity.</returns>
    Task<VehicleId> ConnectAsync(
        SimulatorProfile profile,
        SimulationEndpoint endpoint,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);

    /// <summary>Disconnects only the exact connection owned by this coordinator.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task DisconnectAsync(CancellationToken cancellationToken = default);
}
