using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Library.EventHub.Events;

namespace MissionPlanner.Core.DomainEvents;

/// <summary>
/// Event that is triggered when a vehicle's connection state changes.
/// </summary>
public class VehicleConnectionStateChanged : DomainEvent<VehicleConnectionStateChange>
{
    /// <inheritdoc />
    public VehicleConnectionStateChanged(VehicleConnectionStateChange data)
        : base("VehicleConnectionStateChanged", data)
    {
    }

    public VehicleConnectionStateChange VehicleConnectionStateChange => (VehicleConnectionStateChange)Payload!;

    /// <summary>
    /// Gets the vehicle ID associated with the domain event.
    /// </summary>
    public VehicleId VehicleId => VehicleConnectionStateChange.VehicleId;

    /// <summary>
    /// Gets the previous connection state of the vehicle.
    /// </summary>
    public VehicleConnectionState PreviousState => VehicleConnectionStateChange.PreviousState;

    /// <summary>
    /// Gets the current connection state of the vehicle.
    /// </summary>
    public VehicleConnectionState CurrentState => VehicleConnectionStateChange.CurrentState;

    /// <summary>
    /// Gets the timestamp when the connection state change occurred.
    /// </summary>
    public DateTimeOffset ChangedAt => VehicleConnectionStateChange.ChangedAt;
}
