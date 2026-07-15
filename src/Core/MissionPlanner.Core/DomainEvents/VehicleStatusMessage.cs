using MissionPlanner.Core.Vehicles.Models;

namespace MissionPlanner.Core.DomainEvents;

/// <summary>
/// Represents a status message from a vehicle, including its connection state and the timestamp of the message.
/// </summary>
/// <param name="VehicleId">The ID of the vehicle.</param>
/// <param name="PreviousState">The previous connection state of the vehicle.</param>
/// <param name="CurrentState">The current connection state of the vehicle.</param>
/// <param name="ChangedAt">The timestamp when the status message was received.</param>
public record VehicleStatusMessage(VehicleId VehicleId, VehicleConnectionState PreviousState, VehicleConnectionState CurrentState, DateTimeOffset ChangedAt);
