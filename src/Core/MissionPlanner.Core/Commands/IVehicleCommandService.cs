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

    /// <summary>Disarms a vehicle after the caller has completed any required safety confirmation.</summary>
    /// <param name="vehicleId">The target vehicle.</param>
    /// <param name="safetyConfirmed">Whether the user explicitly confirmed a hazardous disarm.</param>
    /// <param name="cancellationToken">A token that cancels the command.</param>
    /// <returns>The acknowledged command response.</returns>
    Task<VehicleCommandResponse> DisarmAsync(VehicleId vehicleId, bool safetyConfirmed, CancellationToken cancellationToken);


    /// <summary>
    /// Lands the specified vehicle.
    /// </summary>
    /// <param name="state">The current state of the vehicle to land.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task<VehicleCommandResponse> LandAsync(VehicleState state, CancellationToken cancellationToken);

    /// <summary>Sends a family-specific land-mode command to a vehicle.</summary>
    /// <param name="vehicleId">The target vehicle.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The acknowledged command response.</returns>
    Task<VehicleCommandResponse> LandAsync(VehicleId vehicleId, CancellationToken cancellationToken);

    /// <summary>
    /// Sets the mode of the specified vehicle.
    /// </summary>
    /// <param name="vehicleId">The ID of the vehicle to set the mode for.</param>
    /// <param name="mode">The mode to set.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task<VehicleCommandResponse> SetModeAsync(VehicleId vehicleId, VehicleMode mode, CancellationToken cancellationToken);

    /// <summary>Sets a firmware-family-specific mode.</summary>
    /// <param name="vehicleId">The target vehicle.</param>
    /// <param name="mode">The selected mode from the family catalog.</param>
    /// <param name="cancellationToken">A token that cancels the command.</param>
    /// <returns>The acknowledged command response.</returns>
    Task<VehicleCommandResponse> SetModeAsync(VehicleId vehicleId, VehicleModeOption mode, CancellationToken cancellationToken);

    /// <summary>Starts a typed automatic takeoff.</summary>
    /// <param name="vehicleId">The target vehicle.</param>
    /// <param name="altitudeMeters">The target relative altitude in metres.</param>
    /// <param name="safetyConfirmed">Whether the takeoff confirmation was accepted.</param>
    /// <param name="cancellationToken">A token that cancels the command.</param>
    /// <returns>The acknowledged command response.</returns>
    Task<VehicleCommandResponse> TakeoffAsync(VehicleId vehicleId, double altitudeMeters, bool safetyConfirmed, CancellationToken cancellationToken);

    /// <summary>Commands return to launch.</summary>
    /// <param name="vehicleId">The target vehicle.</param>
    /// <param name="cancellationToken">A token that cancels the command.</param>
    /// <returns>The acknowledged command response.</returns>
    Task<VehicleCommandResponse> ReturnToLaunchAsync(VehicleId vehicleId, CancellationToken cancellationToken);

    /// <summary>Commands the family-appropriate loiter or hold mode.</summary>
    /// <param name="vehicleId">The target vehicle.</param>
    /// <param name="cancellationToken">A token that cancels the command.</param>
    /// <returns>The acknowledged command response.</returns>
    Task<VehicleCommandResponse> HoldAsync(VehicleId vehicleId, CancellationToken cancellationToken);

    /// <summary>Reboots the autopilot after explicit safety confirmation.</summary>
    /// <param name="vehicleId">The target vehicle.</param>
    /// <param name="safetyConfirmed">Whether the user confirmed the reboot.</param>
    /// <param name="cancellationToken">A token that cancels the command.</param>
    /// <returns>The acknowledged command response.</returns>
    Task<VehicleCommandResponse> RebootAutopilotAsync(VehicleId vehicleId, bool safetyConfirmed, CancellationToken cancellationToken);

    /// <summary>Sets home to the vehicle's current position after explicit safety confirmation.</summary>
    /// <param name="vehicleId">The target vehicle.</param>
    /// <param name="safetyConfirmed">Whether the user confirmed changing home.</param>
    /// <param name="cancellationToken">A token that cancels the command.</param>
    /// <returns>The acknowledged command response.</returns>
    Task<VehicleCommandResponse> SetHomeHereAsync(VehicleId vehicleId, bool safetyConfirmed, CancellationToken cancellationToken);

    /// <summary>Executes a validated advanced command after explicit safety confirmation.</summary>
    /// <param name="command">The validated expert command.</param>
    /// <param name="safetyConfirmed">Whether the user confirmed expert execution.</param>
    /// <param name="cancellationToken">A token that cancels the command.</param>
    /// <returns>The acknowledged command response.</returns>
    Task<VehicleCommandResponse> ExecuteExpertAsync(ExpertVehicleCommand command, bool safetyConfirmed, CancellationToken cancellationToken);
}
