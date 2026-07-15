using MissionPlanner.Core.Models;

namespace MissionPlanner.Core.DomainEvents;

/// <summary>
/// Represents a change in the connection state of a vehicle.
/// </summary>
/// <param name="VehicleId">The ID of the vehicle.</param>
/// <param name="PreviousState">The previous connection state of the vehicle.</param>
/// <param name="CurrentState">The current connection state of the vehicle.</param>
/// <param name="ChangedAt">The timestamp when the connection state change occurred.</param>
public record VehicleConnectionStateChange(VehicleId VehicleId, VehicleConnectionState PreviousState, VehicleConnectionState CurrentState, DateTimeOffset ChangedAt);
