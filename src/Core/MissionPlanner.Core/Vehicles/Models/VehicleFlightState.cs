namespace MissionPlanner.Core.Vehicles.Models;

public sealed record VehicleFlightState(
    uint CustomMode,
    byte BaseMode,
    byte SystemStatus,
    VehicleMode Mode,
    bool IsArmed);
