namespace MissionPlanner.Core.Vehicles.Models;

/// <summary>
/// Provides the public API for VehicleFlightState.
/// </summary>
/// <param name="CustomMode">The CustomMode value.</param>
/// <param name="BaseMode">The BaseMode value.</param>
/// <param name="SystemStatus">The SystemStatus value.</param>
/// <param name="Mode">The Mode value.</param>
/// <param name="IsArmed">The IsArmed value.</param>
public sealed record VehicleFlightState(
    uint CustomMode,
    byte BaseMode,
    byte SystemStatus,
    VehicleMode Mode,
    bool IsArmed);
