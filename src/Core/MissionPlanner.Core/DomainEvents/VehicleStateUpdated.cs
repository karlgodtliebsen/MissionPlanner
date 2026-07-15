using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Library.EventHub.Events;

namespace MissionPlanner.Core.DomainEvents;

/// <summary>
/// Event that is triggered when a vehicle's state is updated.
/// </summary>
public class VehicleStateUpdated : DomainEvent<VehicleState>
{
    /// <inheritdoc />
    public VehicleStateUpdated(VehicleState data) : base("VehicleStateUpdated", data)
    {
    }

    /// <summary>
    /// Gets the vehicle ID associated with the domain event.
    /// </summary>
    public VehicleId VehicleId => ((VehicleState)Payload!).VehicleId;

    /// <summary>
    /// Gets the vehicle state associated with the domain event.
    /// </summary>
    public VehicleState VehicleState => (VehicleState)Payload!;
}
