using MissionPlanner.Core.Models;

namespace MissionPlanner.Core.DomainEvents;

/// <summary>
/// Data payload for VehicleDisconnected event.
/// </summary>
public record VehicleDisconnectedData(VehicleId VehicleId, DateTimeOffset DisconnectedAt, string? Reason);
