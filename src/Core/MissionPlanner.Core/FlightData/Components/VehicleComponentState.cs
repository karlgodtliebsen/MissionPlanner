namespace MissionPlanner.Core.FlightData.Components;

/// <summary>Describes the latest discovery evidence for a vehicle component.</summary>
public sealed record VehicleComponentState(
    VehicleComponentKey Key,
    byte MavType,
    byte Autopilot,
    DateTimeOffset FirstSeen,
    DateTimeOffset LastSeen,
    bool IsOnline);
