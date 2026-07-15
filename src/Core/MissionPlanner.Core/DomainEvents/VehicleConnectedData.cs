using MissionPlanner.Core.Models;

namespace MissionPlanner.Core.DomainEvents;

/// <summary>
/// Data payload for VehicleConnected event.
/// </summary>
public record VehicleConnectedData(VehicleId VehicleId, string ConnectionType, string Endpoint, DateTimeOffset ConnectedAt);
