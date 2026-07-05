using System.Net;
using MissionPlanner.Core.DomainEvents;
using MissionPlanner.Core.Models;
using MissionPlanner.MavLink.Messages;

namespace MissionPlanner.Core.Services;

/// <summary>
/// Represents a session for a vehicle, managing its state and handling updates.
/// </summary>
public class VehicleSession(VehicleState initialState, IPEndPoint ipEndPoint)
{
    private VehicleState state = initialState;
    private const byte MavModeFlagSafetyArmed = 0b1000_0000;

    /// <summary>
    /// Gets the unique identifier of the vehicle.
    /// </summary>
    public VehicleId Id => state.VehicleId;

    /// <summary>
    /// Gets the current state of the vehicle.
    /// </summary>
    public VehicleState State => state;

    /// <summary>
    /// Gets the IP endpoint of the vehicle.
    /// </summary>
    public IPEndPoint IpEndPoint => ipEndPoint;


    /// <summary>
    /// Gets the notifications for the vehicle.
    /// </summary>
    public IList<VehicleStatusText> Notifications { get; private set; } = [];


    /// <summary>
    /// Updates the connection state of the vehicle based on the elapsed time since the last heartbeat.
    /// </summary>
    /// <param name="now">The current date and time.</param>
    /// <param name="staleAfter">The time span after which the vehicle is considered stale.</param>
    /// <param name="degradedAfter">The time span after which the vehicle is considered degraded.</param>
    /// <param name="offlineAfter">The time span after which the vehicle is considered offline.</param>
    public VehicleConnectionStateChanged? UpdateConnectionState(DateTimeOffset now, TimeSpan staleAfter, TimeSpan degradedAfter, TimeSpan offlineAfter)
    {
        var previousState = state.ConnectionState;

        var age = now - state.LastHeartbeatAt;

        var currentState =
            age > offlineAfter
                ? VehicleConnectionState.Offline
                : age > degradedAfter
                    ? VehicleConnectionState.Degraded
                    : age > staleAfter
                        ? VehicleConnectionState.Stale
                        : VehicleConnectionState.Online;

        state = state with
        {
            ConnectionState = currentState
        };

        if (previousState == currentState) return null;

        var stateChanged = new VehicleConnectionStateChanged(new VehicleConnectionStateChange(state.VehicleId, previousState, currentState, now));
        return stateChanged;
    }


    /// <summary>
    /// Applies a heartbeat message to update the state of the vehicle.
    /// </summary>
    /// <param name="customMode">The custom mode of the vehicle.</param>
    /// <param name="vehicleType">The type of the vehicle.</param>
    /// <param name="autopilot">The autopilot type of the vehicle.</param>
    /// <param name="baseMode">The base mode of the vehicle.</param>
    /// <param name="systemStatus">The system status of the vehicle.</param>
    /// <param name="mavLinkVersion">The MAVLink version of the vehicle.</param>
    /// <param name="receivedAt">The timestamp when the heartbeat was received.</param>
    public void ApplyHeartbeat(
        uint customMode,
        byte vehicleType,
        byte autopilot,
        byte baseMode,
        byte systemStatus,
        byte mavLinkVersion,
        DateTimeOffset receivedAt)
    {
        state = state with
        {
            CustomMode = customMode,
            VehicleType = vehicleType,
            Autopilot = autopilot,
            BaseMode = baseMode,
            SystemStatus = systemStatus,
            MavLinkVersion = mavLinkVersion,
            Mode = MapMode(customMode),
            IsArmed = (baseMode & MavModeFlagSafetyArmed) != 0,
            ConnectionState = VehicleConnectionState.Online,
            LastHeartbeatAt = receivedAt
        };
    }

    /// <summary>
    /// Applies position information to the vehicle's state.
    /// </summary>
    /// <param name="latitude">The latitude of the vehicle.</param>
    /// <param name="longitude">The longitude of the vehicle.</param>
    /// <param name="altitude">The altitude of the vehicle.</param>
    public void ApplyPosition(double latitude, double longitude, double altitude)
    {
        state = state with
        {
            Latitude = latitude,
            Longitude = longitude,
            Altitude = altitude
        };
    }

    /// <summary>
    /// Applies attitude information to the vehicle's state.
    /// </summary>
    /// <param name="roll">The roll angle of the vehicle.</param>
    /// <param name="pitch">The pitch angle of the vehicle.</param>
    /// <param name="yaw">The yaw angle of the vehicle.</param>
    public void ApplyAttitude(double roll, double pitch, double yaw)
    {
        state = state with
        {
            Roll = roll,
            Pitch = pitch,
            Yaw = yaw
        };
    }

    /// <summary>
    /// Applies battery information to the vehicle's state.
    /// </summary>
    /// <param name="batteryRemaining">The remaining battery percentage.</param>
    /// <param name="batteryVoltage">The battery voltage.</param>
    public void ApplyBattery(int? batteryRemaining, float? batteryVoltage)
    {
        state = state with
        {
            BatteryRemaining = batteryRemaining,
            BatteryVoltage = batteryVoltage
        };
    }

    /// <summary>
    /// Applies the armed state to the vehicle's state.
    /// </summary>
    /// <param name="isArmed">Indicates whether the vehicle is armed.</param>
    public void ApplyArm(bool isArmed)
    {
        state = state with
        {
            IsArmed = isArmed
        };
    }

    /// <summary>
    /// Applies the mode to the vehicle's state.
    /// </summary>
    /// <param name="mode">The mode to apply.</param>
    public void ApplyMode(VehicleMode mode)
    {
        state = state with
        {
            Mode = mode
        };
    }

    /// <summary>
    /// Applies a status text message to the vehicle's state.
    /// </summary>
    /// <param name="message">The status text message to apply.</param>
    public void ApplyStatusText(StatusTextMessage message)
    {
        Notifications.Add(new VehicleStatusText(message.SystemId, message.ComponentId, message.Text, DateTimeOffset.UtcNow));
    }


    private static VehicleMode MapMode(uint customMode)
    {
        return customMode switch
        {
            0 => VehicleMode.Stabilize,
            2 => VehicleMode.AltHold,
            4 => VehicleMode.Guided,
            5 => VehicleMode.Loiter,
            6 => VehicleMode.Rtl,
            9 => VehicleMode.Land,
            var _ => VehicleMode.Unknown
        };
    }
}