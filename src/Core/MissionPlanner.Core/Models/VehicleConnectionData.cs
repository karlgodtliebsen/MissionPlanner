namespace MissionPlanner.Core.Models;

public sealed record VehicleConnectionData(
    VehicleConnectionState State,
    DateTimeOffset LastHeartbeatAt);
