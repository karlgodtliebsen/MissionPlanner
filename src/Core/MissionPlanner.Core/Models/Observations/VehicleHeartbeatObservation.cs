namespace MissionPlanner.Core.Models.Observations;

public sealed record VehicleHeartbeatObservation(
    uint CustomMode,
    byte VehicleType,
    byte Autopilot,
    byte BaseMode,
    byte SystemStatus,
    byte MavLinkVersion,
    DateTimeOffset ObservedAt) : IVehicleObservation;
