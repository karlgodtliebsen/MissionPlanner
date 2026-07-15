using MissionPlanner.Core.Commands;
using MissionPlanner.Core.Models;

namespace MissionPlanner.Core.Services.Abstractions;

/// <summary>
/// Defines the policy for validating vehicle commands.
/// </summary>
public interface IVehicleCommandPolicy
{
    /// <summary>
    /// Validates whether a vehicle can be armed based on its current state.
    /// </summary>
    /// <param name="state">The current state of the vehicle.</param>
    /// <returns>A <see cref="VehicleCommandResponse"/> indicating the result of the validation, or null if the command is allowed.</returns>
    VehicleCommandResponse? ValidateArm(VehicleState state);

    /// <summary>
    /// Validates whether a vehicle can be disarmed based on its current state.
    /// </summary>
    /// <param name="state">The current state of the vehicle.</param>
    /// <returns>A <see cref="VehicleCommandResponse"/> indicating the result of the validation, or null if the command is allowed.</returns>
    VehicleCommandResponse? ValidateDisarm(VehicleState state);

    /// <summary>
    /// Validates whether a vehicle can change its mode based on its current state.
    /// </summary>
    /// <param name="state">The current state of the vehicle.</param>
    /// <param name="mode">The desired mode to set.</param>
    /// <returns>A <see cref="VehicleCommandResponse"/> indicating the result of the validation, or null if the command is allowed.</returns>
    VehicleCommandResponse? ValidateSetMode(VehicleState state, VehicleMode mode);
}
