namespace MissionPlanner.Core.Vehicles;

/// <summary>
/// Represents a status text message from a vehicle.
/// </summary>
/// <param name="SystemId">The ID of the system that sent the message.</param>
/// <param name="ComponentId">The ID of the component that sent the message.</param>
/// <param name="Text">The text of the message.</param>
/// <param name="ReceivedAt">The timestamp when the message was received.</param>
public record VehicleStatusText(byte SystemId, byte ComponentId, string Text, DateTimeOffset ReceivedAt);
