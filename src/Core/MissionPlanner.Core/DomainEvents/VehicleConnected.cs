using MissionPlanner.Core.Vehicles.Models;
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

    /// <summary>
    /// Initializes a new instance of the <see cref="VehicleConnected"/> class.
    /// </summary>
    /// <param name="vehicleId">The ID of the connected vehicle.</param>
    /// <param name="connectionType">The type of connection used.</param>
    /// <param name="endpoint">The endpoint of the connection.</param>
    /// <param name="connectedAt">The timestamp when the vehicle connected.</param>
    public VehicleConnected(VehicleId vehicleId, string connectionType, string endpoint, DateTimeOffset connectedAt)
        : base("VehicleConnected", new VehicleConnectedData(vehicleId, connectionType, endpoint, connectedAt))
    {
    }
}
