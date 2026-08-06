using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Shared.Models.Vehicles.Models;

namespace MissionPlanner.Core.DomainEvents;

/// <summary>
/// Data payload for VehicleDisconnected event.
/// </summary>
public record VehicleDisconnectedData(VehicleId VehicleId, DateTimeOffset DisconnectedAt, string? Reason);
