namespace MissionPlanner.Core.Vehicles.Models;

public sealed record VehicleConnectionData(
    VehicleConnectionState State,
    DateTimeOffset LastHeartbeatAt);
