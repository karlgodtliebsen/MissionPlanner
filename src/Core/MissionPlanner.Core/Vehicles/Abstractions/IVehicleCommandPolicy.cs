using MissionPlanner.Core.Commands;
using MissionPlanner.Core.Vehicles.Models;

namespace MissionPlanner.Core.Vehicles.Abstractions;

/// <summary>
/// Defines the policy for validating vehicle commands.
/// </summary>
public interface IVehicleCommandPolicy
{
    /// <summary>
    /// Evaluates whether an action is safe for the current immutable vehicle state.
    /// </summary>
    /// <param name="state">The current vehicle state.</param>
    /// <param name="action">The requested action.</param>
    /// <returns>The policy decision and user-facing reason.</returns>
    VehicleCommandDecision Evaluate(VehicleState state, VehicleAction action);

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
