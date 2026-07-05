using Domain.Library.EventHub.Events;
using MissionPlanner.Core.Models;

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