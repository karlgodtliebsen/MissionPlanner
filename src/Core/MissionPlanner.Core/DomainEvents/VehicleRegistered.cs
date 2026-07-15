using MissionPlanner.Core.Models;
using MissionPlanner.Library.EventHub.Events;

namespace MissionPlanner.Core.DomainEvents;

/// <summary>
/// Event that is triggered when a vehicle is registered.
/// </summary>
public class VehicleRegistered : DomainEvent<VehicleId>
{
    /// <summary>
    /// 
    /// </summary>
    public VehicleId VehicleId => (VehicleId)Payload!;

    /// <inheritdoc />
    public VehicleRegistered(VehicleId data) : base("VehicleRegistered", data)
    {
    }
}
