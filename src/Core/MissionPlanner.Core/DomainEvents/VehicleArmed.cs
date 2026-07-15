using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Library.EventHub.Events;

namespace MissionPlanner.Core.DomainEvents;

/// <summary>
/// Event that is triggered when a vehicle is armed 
/// </summary>
public class VehicleArmed : DomainEvent<VehicleId>
{
    /// <inheritdoc />
    public VehicleArmed(VehicleId vehicleId)
        : base("VehicleArmed", vehicleId)
    {
    }

    /// <summary>
    /// Gets the ID of the vehicle that was armed.
    /// </summary>
    public VehicleId VehicleId => (VehicleId)Payload!;
}
