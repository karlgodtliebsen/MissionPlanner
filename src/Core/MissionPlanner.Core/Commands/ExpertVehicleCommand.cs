using MissionPlanner.Core.Vehicles.Models;

namespace MissionPlanner.Core.Commands;

/// <summary>
/// Represents a validated advanced COMMAND_LONG request.
/// </summary>
/// <param name="VehicleId">The target vehicle.</param>
/// <param name="CommandId">The MAV_CMD identifier.</param>
/// <param name="Parameters">Exactly seven finite COMMAND_LONG parameters.</param>
public sealed record ExpertVehicleCommand(VehicleId VehicleId, ushort CommandId, IReadOnlyList<float> Parameters);
