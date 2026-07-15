using MissionPlanner.Core.Vehicles.Models;

namespace MissionPlanner.Simulator;

/// <summary>
/// A factory class for creating <see cref="VehicleState"/> instances from legacy state objects.
/// </summary>
public static class VehicleStateFactory
{
    /// <summary>
    /// Creates a new <see cref="VehicleState"/> instance from a legacy state object with a specified custom mode and vehicle mode.
    /// </summary>
    /// <param name="state">The legacy vehicle state.</param>
    /// <param name="customMode">The custom mode to set.</param>
    /// <param name="mode">The vehicle mode to set.</param>
    /// <returns>A new <see cref="VehicleState"/> instance.</returns>
    /// <exception cref="ArgumentNullException"></exception>
    public static VehicleState CreateFromLegacyState(VehicleState state, byte customMode, VehicleMode mode)
    {
        return state is null
            ? throw new ArgumentNullException(nameof(state))
            : new VehicleState(state.VehicleId, state.CustomMode, state.VehicleType, state.Autopilot, customMode, state.SystemStatus, state.MavLinkVersion, state.ConnectionState,
                state.LastHeartbeatAt, mode, state.IsArmed, state.Latitude, state.Longitude, state.Altitude, state.Roll, state.Pitch, state.Yaw,
                state.BatteryRemaining,
                state.BatteryVoltage);
    }

    /// <summary>
    /// Creates a new <see cref="VehicleState"/> instance from a legacy state object with a specified base mode and armed state.
    /// </summary>
    /// <param name="state">The legacy vehicle state.</param>
    /// <param name="newBaseMode">The new base mode to set.</param>
    /// <param name="isArmed">Whether the vehicle is armed.</param>
    /// <returns>A new <see cref="VehicleState"/> instance.</returns>
    /// <exception cref="ArgumentNullException"></exception>
    public static VehicleState CreateFromLegacyState(VehicleState state, byte newBaseMode, bool isArmed)
    {
        return state is null
            ? throw new ArgumentNullException(nameof(state))
            : new VehicleState(state.VehicleId, state.CustomMode, state.VehicleType, state.Autopilot, newBaseMode, state.SystemStatus, state.MavLinkVersion, state.ConnectionState,
                state.LastHeartbeatAt, state.Mode, isArmed, state.Latitude, state.Longitude, state.Altitude, state.Roll, state.Pitch, state.Yaw,
                state.BatteryRemaining,
                state.BatteryVoltage);
    }
}
