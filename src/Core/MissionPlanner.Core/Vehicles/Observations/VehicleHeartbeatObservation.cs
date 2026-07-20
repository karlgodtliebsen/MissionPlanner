using MissionPlanner.Core.Vehicles.Abstractions;

namespace MissionPlanner.Core.Vehicles.Observations;

/// <summary>
/// Provides the public API for VehicleHeartbeatObservation.
/// </summary>
/// <param name="CustomMode">The CustomMode value.</param>
/// <param name="VehicleType">The VehicleType value.</param>
/// <param name="Autopilot">The Autopilot value.</param>
/// <param name="BaseMode">The BaseMode value.</param>
/// <param name="SystemStatus">The SystemStatus value.</param>
/// <param name="MavLinkVersion">The MavLinkVersion value.</param>
/// <param name="ObservedAt">The ObservedAt value.</param>
public sealed record VehicleHeartbeatObservation(
    uint CustomMode,
    byte VehicleType,
    byte Autopilot,
    byte BaseMode,
    byte SystemStatus,
    byte MavLinkVersion,
    DateTimeOffset ObservedAt) : IVehicleObservation;
