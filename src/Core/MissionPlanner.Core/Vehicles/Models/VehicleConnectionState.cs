namespace MissionPlanner.Core.Vehicles.Models;

/// <summary>
/// Represents the connection state of a vehicle.
/// </summary>
public enum VehicleConnectionState
{
    Unknown = 0,
    Online = 1,
    Stale = 2,
    Degraded = 3,
    Offline = 4
}
