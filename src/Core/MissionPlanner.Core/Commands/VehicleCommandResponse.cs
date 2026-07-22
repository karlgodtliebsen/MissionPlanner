using MissionPlanner.Core.Vehicles.Models;

namespace MissionPlanner.Core.Commands;

/// <summary>
/// Represents the response of a vehicle command.
/// </summary>
/// <param name="VehicleId">The ID of the vehicle.</param>
/// <param name="Result">The result of the command.</param>
/// <param name="CompletedAt">The timestamp when the command was completed.</param>
/// <param name="Message">The optional denial, acknowledgement, or diagnostic detail.</param>
public sealed record VehicleCommandResponse(VehicleId VehicleId, VehicleCommandResult Result, DateTimeOffset CompletedAt, string? Message = null);
