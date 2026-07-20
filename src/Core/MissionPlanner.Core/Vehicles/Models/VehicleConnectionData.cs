namespace MissionPlanner.Core.Vehicles.Models;

/// <summary>
/// Provides the public API for VehicleConnectionData.
/// </summary>
/// <param name="State">The State value.</param>
/// <param name="LastHeartbeatAt">The LastHeartbeatAt value.</param>
public sealed record VehicleConnectionData(
    VehicleConnectionState State,
    DateTimeOffset LastHeartbeatAt);
