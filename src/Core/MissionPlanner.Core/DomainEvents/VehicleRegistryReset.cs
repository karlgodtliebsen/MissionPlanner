using MissionPlanner.Library.EventHub.Events;

namespace MissionPlanner.Core.DomainEvents;

/// <summary>
/// Event that is triggered when a vehicle registry is reset.
/// </summary>
public class VehicleRegistryReset : DomainEvent
{
    /// <inheritdoc />
    public VehicleRegistryReset() : base("VehicleRegistryReset")
    {
    }
}
