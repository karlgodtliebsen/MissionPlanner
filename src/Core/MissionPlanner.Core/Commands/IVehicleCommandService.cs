using MissionPlanner.Core.Vehicles.Models;

namespace MissionPlanner.Core.Commands;

/// <summary>
/// Defines the contract for a service that can send commands to a vehicle.
/// </summary>
public interface IVehicleCommandService : IAsyncDisposable
{
    /// <summary>
    /// Arms the specified vehicle.
    /// </summary>
    /// <param name="vehicleId">The ID of the vehicle to arm.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task<VehicleCommandResponse> ArmAsync(VehicleId vehicleId, CancellationToken cancellationToken);


    /// <summary>
    /// Disarms the specified vehicle.
    /// </summary>
    /// <param name="vehicleId">The ID of the vehicle to disarm.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task<VehicleCommandResponse> DisarmAsync(VehicleId vehicleId, CancellationToken cancellationToken);


    /// <summary>
    /// Lands the specified vehicle.
    /// </summary>
    /// <param name="state">The current state of the vehicle to land.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task<VehicleCommandResponse> LandAsync(VehicleState state, CancellationToken cancellationToken);

    /// <summary>
    /// Sets the mode of the specified vehicle.
    /// </summary>
    /// <param name="vehicleId">The ID of the vehicle to set the mode for.</param>
    /// <param name="mode">The mode to set.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task<VehicleCommandResponse> SetModeAsync(VehicleId vehicleId, VehicleMode mode, CancellationToken cancellationToken);
}
