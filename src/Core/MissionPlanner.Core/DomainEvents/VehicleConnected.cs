using Domain.Library.EventHub.Events;
using MissionPlanner.Core.Models;

namespace MissionPlanner.Core.DomainEvents;

/// <summary>
/// Domain event published when a vehicle successfully connects.
/// </summary>
public class VehicleConnected : DomainEvent<VehicleConnectedData>
{
    public VehicleId VehicleId => ((VehicleConnectedData)Payload!).VehicleId;
    public string ConnectionType => ((VehicleConnectedData)Payload!).ConnectionType;
    public string Endpoint => ((VehicleConnectedData)Payload!).Endpoint;
    public DateTimeOffset ConnectedAt => ((VehicleConnectedData)Payload!).ConnectedAt;

    public VehicleConnected(VehicleId vehicleId, string connectionType, string endpoint, DateTimeOffset connectedAt)
        : base("VehicleConnected", new VehicleConnectedData(vehicleId, connectionType, endpoint, connectedAt))
    {
    }
}

/// <summary>
/// Data payload for VehicleConnected event.
/// </summary>
public record VehicleConnectedData(
    VehicleId VehicleId,
    string ConnectionType,
    string Endpoint,
    DateTimeOffset ConnectedAt);
