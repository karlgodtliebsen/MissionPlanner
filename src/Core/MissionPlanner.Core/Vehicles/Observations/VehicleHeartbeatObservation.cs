using MissionPlanner.Core.Vehicles.Abstractions;

namespace MissionPlanner.Core.Vehicles.Observations;

public sealed record VehicleHeartbeatObservation(
    uint CustomMode,
    byte VehicleType,
    byte Autopilot,
    byte BaseMode,
    byte SystemStatus,
    byte MavLinkVersion,
    DateTimeOffset ObservedAt) : IVehicleObservation;
