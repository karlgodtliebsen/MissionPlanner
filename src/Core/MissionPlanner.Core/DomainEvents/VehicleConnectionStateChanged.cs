using MissionPlanner.Core.Models;
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

/// <summary>
/// Represents a change in the connection state of a vehicle.
/// </summary>
/// <param name="VehicleId">The ID of the vehicle.</param>
/// <param name="PreviousState">The previous connection state of the vehicle.</param>
/// <param name="CurrentState">The current connection state of the vehicle.</param>
/// <param name="ChangedAt">The timestamp when the connection state change occurred.</param>
public record VehicleConnectionStateChange(VehicleId VehicleId, VehicleConnectionState PreviousState, VehicleConnectionState CurrentState, DateTimeOffset ChangedAt);


//public sealed class VehicleConnectionStateProjection
//{
//    public async Task Handle(VehicleConnectionStateChanged domainEvent, CancellationToken cancellationToken)
//    {
//        if (domainEvent.NewState == VehicleConnectionState.Online)
//        {
//            await eventHub.PublishAsync(nameof(VehicleConnected), new VehicleConnected(domainEvent.VehicleId, domainEvent.OccurredAt), cancellationToken);
//        }

//        if (domainEvent.NewState == VehicleConnectionState.Offline)
//        {
//            await eventHub.PublishAsync(nameof(VehicleDisconnected), new VehicleDisconnected(domainEvent.VehicleId, domainEvent.OccurredAt), cancellationToken);
//        }
//    }
//}
