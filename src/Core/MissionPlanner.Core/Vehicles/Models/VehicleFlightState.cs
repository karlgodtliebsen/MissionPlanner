namespace MissionPlanner.Core.Vehicles.Models;

/// <summary>
/// Provides the public API for VehicleFlightState.
/// </summary>
/// <param name="CustomMode">The CustomMode value.</param>
/// <param name="BaseMode">The BaseMode value.</param>
/// <param name="SystemStatus">The SystemStatus value.</param>
/// <param name="Mode">The Mode value.</param>
/// <param name="IsArmed">The IsArmed value.</param>
/// <param name="VtolState">The current VTOL transition state.</param>
/// <param name="LandedState">The current landed state.</param>
/// <param name="ObservedAt">The time at which the extended flight state was observed.</param>
public sealed record VehicleFlightState(
    uint CustomMode,
    byte BaseMode,
    byte SystemStatus,
    VehicleMode Mode,
    bool IsArmed,
    VehicleVtolState VtolState = VehicleVtolState.Undefined,
    VehicleLandedState LandedState = VehicleLandedState.Undefined,
    DateTimeOffset? ObservedAt = null)
{
    /// <summary>Returns whether extended flight state is older than <paramref name="maximumAge"/>.</summary>
    public bool IsStale(DateTimeOffset now, TimeSpan maximumAge) => ObservedAt is null || now - ObservedAt > maximumAge;
}
