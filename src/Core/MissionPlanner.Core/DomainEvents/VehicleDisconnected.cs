using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Library.EventHub.Events;

namespace MissionPlanner.Core.DomainEvents;

/// <summary>
/// Domain event published when a vehicle disconnects.
/// </summary>
public class VehicleDisconnected : DomainEvent<VehicleDisconnectedData>
{
    /// <summary>
    /// Provides the public API for VehicleId.
    /// </summary>
    public VehicleId VehicleId => ((VehicleDisconnectedData)Payload!).VehicleId;
    /// <summary>
    /// Provides the public API for DisconnectedAt.
    /// </summary>
    public DateTimeOffset DisconnectedAt => ((VehicleDisconnectedData)Payload!).DisconnectedAt;
    /// <summary>
    /// Provides the public API for Reason.
    /// </summary>
    public string? Reason => ((VehicleDisconnectedData)Payload!).Reason;

    /// <summary>
    /// Provides the public API for VehicleDisconnected.
    /// </summary>
    public VehicleDisconnected(VehicleId vehicleId, DateTimeOffset disconnectedAt, string? reason = null)
        : base("VehicleDisconnected", new VehicleDisconnectedData(vehicleId, disconnectedAt, reason))
    {
    }
}
