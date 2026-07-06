using MissionPlanner.Core.Models;
using MissionPlanner.Library.EventHub.Events;

namespace MissionPlanner.Core.DomainEvents;

/// <summary>
/// Domain event published when a vehicle disconnects.
/// </summary>
public class VehicleDisconnected : DomainEvent<VehicleDisconnectedData>
{
    public VehicleId VehicleId => ((VehicleDisconnectedData)Payload!).VehicleId;
    public DateTimeOffset DisconnectedAt => ((VehicleDisconnectedData)Payload!).DisconnectedAt;
    public string? Reason => ((VehicleDisconnectedData)Payload!).Reason;

    public VehicleDisconnected(VehicleId vehicleId, DateTimeOffset disconnectedAt, string? reason = null)
        : base("VehicleDisconnected", new VehicleDisconnectedData(vehicleId, disconnectedAt, reason))
    {
    }
}

/// <summary>
/// Data payload for VehicleDisconnected event.
/// </summary>
public record VehicleDisconnectedData(
    VehicleId VehicleId,
    DateTimeOffset DisconnectedAt,
    string? Reason);
