using MissionPlanner.Core.Vehicles;
using MissionPlanner.Library.EventHub.Events;

namespace MissionPlanner.Core.DomainEvents;

/// <summary>
/// Announces a complete or explicitly truncated vehicle status-text entry.
/// </summary>
public sealed class VehicleStatusTextReceived : DomainEvent<VehicleStatusText>
{
    /// <summary>Initializes a new status-text event.</summary>
    /// <param name="message">The stored status-text entry.</param>
    public VehicleStatusTextReceived(VehicleStatusText message)
        : base(nameof(VehicleStatusTextReceived), message)
    {
    }
}
