namespace MissionPlanner.Core.FlightData.Components;

/// <summary>Contains observed uAvionix transponder state.</summary>
public sealed record TransponderComponentState(
    VehicleComponentKey Key,
    ushort Squawk,
    string FlightId,
    byte State,
    byte Fault,
    byte TemperatureCelsius,
    DateTimeOffset ObservedAt);
