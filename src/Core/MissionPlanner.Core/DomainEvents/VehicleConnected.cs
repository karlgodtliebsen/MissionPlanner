using MissionPlanner.Core.Models;
using MissionPlanner.Library.EventHub.Events;

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
