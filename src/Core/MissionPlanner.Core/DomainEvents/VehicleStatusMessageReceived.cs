using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Library.EventHub.Events;

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
