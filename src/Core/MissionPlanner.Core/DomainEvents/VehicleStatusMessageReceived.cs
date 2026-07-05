using Domain.Library.EventHub.Events;
using MissionPlanner.Core.Models;

namespace MissionPlanner.Core.DomainEvents;

/// <summary>
/// Event that is triggered when a vehicle status message is received, which may indicate a change in the vehicle's connection state or other status information.
/// </summary>
public class VehicleStatusMessageReceived : DomainEvent<VehicleStatusMessage>
{
    /// <inheritdoc />
    public VehicleStatusMessageReceived(VehicleStatusMessage data)
        : base("VehicleStatusMessageReceived", data)
    {
    }

    /// <summary>
    /// Gets the vehicle status message associated with the domain event.
    /// </summary>
    public VehicleStatusMessage VehicleStatusMessage => (VehicleStatusMessage)Payload!;

    /// <summary>
    /// Gets the vehicle ID associated with the domain event.
    /// </summary>
    public VehicleId VehicleId => VehicleStatusMessage.VehicleId;

    /// <summary>
    /// Gets the previous connection state of the vehicle.
    /// </summary>
    public VehicleConnectionState PreviousState => VehicleStatusMessage.PreviousState;


    /// <summary>
    /// Gets the current connection state of the vehicle.
    /// </summary>
    public VehicleConnectionState CurrentState => VehicleStatusMessage.CurrentState;

    /// <summary>
    /// Gets the timestamp when the status message was received.
    /// </summary>
    public DateTimeOffset ChangedAt => VehicleStatusMessage.ChangedAt;
}

/// <summary>
/// Represents a status message from a vehicle, including its connection state and the timestamp of the message.
/// </summary>
/// <param name="VehicleId">The ID of the vehicle.</param>
/// <param name="PreviousState">The previous connection state of the vehicle.</param>
/// <param name="CurrentState">The current connection state of the vehicle.</param>
/// <param name="ChangedAt">The timestamp when the status message was received.</param>
public record VehicleStatusMessage(VehicleId VehicleId, VehicleConnectionState PreviousState, VehicleConnectionState CurrentState, DateTimeOffset ChangedAt);