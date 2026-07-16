using MissionPlanner.Core.DomainEvents;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Core.Vehicles.Observations;
using MissionPlanner.Library.DateTime.Domain;
using MissionPlanner.MavLink.Messages;
using MissionPlanner.Transport;

namespace MissionPlanner.Core.Vehicles;

/// <summary>
/// Represents a session for a vehicle, managing its state and interactions.
/// </summary>
/// <param name="initialState">The initial state of the vehicle.</param>
/// <param name="endPoint">The transport endpoint for communication.</param>
/// <param name="dateTimeProvider">The provider for current date and time.</param>
public class VehicleSession(VehicleState initialState, TransportEndPoint endPoint, IDateTimeProvider dateTimeProvider)
{
    private const byte MavModeFlagSafetyArmed = 0b1000_0000;
    private VehicleState state = initialState;

    public VehicleId Id => state.VehicleId;
    public VehicleState State => state;
    public TransportEndPoint EndPoint => endPoint;
    public IList<VehicleStatusText> Notifications { get; private set; } = [];

    /// <summary>
    /// Updates the connection state based on the last heartbeat timestamp and the provided thresholds for stale, degraded, and offline states.
    /// </summary>
    /// <param name="now"></param>
    /// <param name="staleAfter"></param>
    /// <param name="degradedAfter"></param>
    /// <param name="offlineAfter"></param>
    /// <returns></returns>
    public VehicleConnectionStateChanged? UpdateConnectionState(
        DateTimeOffset now,
        TimeSpan staleAfter,
        TimeSpan degradedAfter,
        TimeSpan offlineAfter)
    {
        var previousState = state.Connection.State;
        var age = now - state.Connection.LastHeartbeatAt;
        var currentState = age > offlineAfter
            ? VehicleConnectionState.Offline
            : age > degradedAfter
                ? VehicleConnectionState.Degraded
                : age > staleAfter
                    ? VehicleConnectionState.Stale
                    : VehicleConnectionState.Online;

        state = state with { Connection = state.Connection with { State = currentState } };
        return previousState == currentState
            ? null
            : new VehicleConnectionStateChanged(
                new VehicleConnectionStateChange(state.VehicleId, previousState, currentState, now));
    }

    /// <summary>
    /// Compatibility methods for gradual migration.
    /// </summary>
    /// <param name="observation"></param>
    public void ApplyHeartbeat(VehicleHeartbeatObservation observation)
    {
        state = state with { Identity = new VehicleIdentityState(observation.VehicleType, observation.Autopilot, observation.MavLinkVersion), Flight = new VehicleFlightState(observation.CustomMode, observation.BaseMode, observation.SystemStatus, MapMode(observation.CustomMode), (observation.BaseMode & MavModeFlagSafetyArmed) != 0), Connection = new VehicleConnectionData(VehicleConnectionState.Online, observation.ObservedAt) };
    }

    /// <summary>
    /// Compatibility methods for gradual migration.
    /// </summary>
    /// <param name="observation"></param>
    public void ApplyAttitude(VehicleAttitudeObservation observation)
    {
        state = state with { Motion = state.Motion with { RollRadians = observation.RollRadians, PitchRadians = observation.PitchRadians, YawRadians = observation.YawRadians, ObservedAt = observation.ObservedAt } };
    }

    /// <summary>
    /// Compatibility methods for gradual migration.
    /// </summary>
    /// <param name="observation"></param>
    public void ApplyGlobalPosition(VehicleGlobalPositionObservation observation)
    {
        var groundSpeed = CalculateHorizontalSpeed(observation.VelocityNorthMetersPerSecond, observation.VelocityEastMetersPerSecond);

        state = state with
        {
            Position = state.Position with
            {
                LatitudeDegrees = observation.LatitudeDegrees,
                LongitudeDegrees = observation.LongitudeDegrees,
                AltitudeMslMeters = observation.AltitudeMslMeters,
                RelativeAltitudeMeters = observation.RelativeAltitudeMeters ?? state.Position.RelativeAltitudeMeters,
                HeadingDegrees = observation.HeadingDegrees ?? state.Position.HeadingDegrees,
                ObservedAt = observation.ObservedAt
            },
            Motion = state.Motion with
            {
                VelocityNorthMetersPerSecond = observation.VelocityNorthMetersPerSecond,
                VelocityEastMetersPerSecond = observation.VelocityEastMetersPerSecond,
                VelocityDownMetersPerSecond = observation.VelocityDownMetersPerSecond,
                GroundSpeedMetersPerSecond = groundSpeed ?? state.Motion.GroundSpeedMetersPerSecond,

                // MAVLink NED velocity is positive downward.
                // The domain convention is positive climb.
                VerticalSpeedMetersPerSecond =
                observation.VelocityDownMetersPerSecond is { } down
                    ? -down
                    : state.Motion.VerticalSpeedMetersPerSecond,
                ObservedAt = observation.ObservedAt
            }
        };
    }

    private static double? CalculateHorizontalSpeed(double? northMetersPerSecond, double? eastMetersPerSecond)
    {
        return northMetersPerSecond is not { } north || eastMetersPerSecond is not { } east ? null : Math.Sqrt(north * north + east * east);
    }

    /// <summary>
    /// Applies the vehicle's home (launch) position from a HOME_POSITION message.
    /// </summary>
    /// <param name="observation">The home position observation.</param>
    public void ApplyHomePosition(VehicleHomePositionObservation observation)
    {
        state = state with
        {
            Position = state.Position with
            {
                HomeLatitudeDegrees = observation.LatitudeDegrees,
                HomeLongitudeDegrees = observation.LongitudeDegrees,
                HomeAltitudeMslMeters = observation.AltitudeMslMeters
            }
        };
    }

    /// <summary>
    /// Compatibility methods for gradual migration.
    /// </summary>
    /// <param name="observation"></param>
    public void ApplyLocalPosition(VehicleLocalPositionObservation observation)
    {
        state = state with
        {
            Position = state.Position with { LocalNorthMeters = observation.NorthMeters, LocalEastMeters = observation.EastMeters, LocalDownMeters = observation.DownMeters, ObservedAt = observation.ObservedAt },
            Motion = state.Motion with
            {
                VelocityNorthMetersPerSecond = observation.VelocityNorthMetersPerSecond,
                VelocityEastMetersPerSecond = observation.VelocityEastMetersPerSecond,
                VelocityDownMetersPerSecond = observation.VelocityDownMetersPerSecond,
                VerticalSpeedMetersPerSecond = -observation.VelocityDownMetersPerSecond,
                ObservedAt = observation.ObservedAt
            }
        };
    }

    /// <summary>
    /// Compatibility methods for gradual migration.
    /// </summary>
    /// <param name="observation"></param>
    public void ApplyGps(VehicleGpsObservation observation)
    {
        state = state with
        {
            Gps = new VehicleGpsState(
                observation.FixType,
                observation.SatellitesVisible,
                observation.HorizontalDilution,
                observation.VerticalDilution,
                observation.GroundSpeedMetersPerSecond,
                observation.CourseDegrees,
                observation.HorizontalAccuracyMeters,
                observation.VerticalAccuracyMeters,
                observation.ObservedAt),
            Motion = state.Motion with
            {
                GroundSpeedMetersPerSecond = observation.GroundSpeedMetersPerSecond
                                             ?? state.Motion.GroundSpeedMetersPerSecond,
                ObservedAt = observation.ObservedAt
            },
            Position = state.Position with { HeadingDegrees = observation.CourseDegrees ?? state.Position.HeadingDegrees, ObservedAt = observation.ObservedAt }
        };
    }

    /// <summary>
    /// Compatibility methods for gradual migration.
    /// </summary>
    /// <param name="observation"></param>
    public void ApplyAhrsFallback(VehicleAhrsObservation observation)
    {
        var attitudeIsStale = state.Motion.ObservedAt is null || observation.ObservedAt - state.Motion.ObservedAt.Value > TimeSpan.FromSeconds(1);

        var positionIsStale = state.Position.ObservedAt is null || observation.ObservedAt - state.Position.ObservedAt.Value > TimeSpan.FromSeconds(1);

        state = state with
        {
            Motion = attitudeIsStale
                ? state.Motion with { RollRadians = observation.RollRadians, PitchRadians = observation.PitchRadians, YawRadians = observation.YawRadians, ObservedAt = observation.ObservedAt }
                : state.Motion,
            Position = positionIsStale
                ? state.Position with { LatitudeDegrees = observation.LatitudeDegrees, LongitudeDegrees = observation.LongitudeDegrees, AltitudeMslMeters = observation.AltitudeMslMeters, ObservedAt = observation.ObservedAt }
                : state.Position
        };
    }

    /// <summary>
    /// Compatibility methods for gradual migration.
    /// </summary>
    /// <param name="observation"></param>
    public void ApplyHud(VehicleHudObservation observation)
    {
        state = state with { Motion = state.Motion with { AirSpeedMetersPerSecond = observation.AirSpeedMetersPerSecond, GroundSpeedMetersPerSecond = observation.GroundSpeedMetersPerSecond, VerticalSpeedMetersPerSecond = observation.VerticalSpeedMetersPerSecond, ObservedAt = observation.ObservedAt }, Position = state.Position with { AltitudeMslMeters = observation.AltitudeMslMeters, HeadingDegrees = observation.HeadingDegrees, ObservedAt = observation.ObservedAt } };
    }

    /// <summary>
    /// Compatibility methods for gradual migration.
    /// </summary>
    /// <param name="observation"></param>
    public void ApplyBattery(VehicleBatteryObservation observation)
    {
        state = state with
        {
            Power = state.Power with
            {
                BatteryVoltageVolts = observation.VoltageVolts ?? state.Power.BatteryVoltageVolts,
                BatteryCurrentAmps = observation.CurrentAmps ?? state.Power.BatteryCurrentAmps,
                BatteryConsumedMah = observation.ConsumedMah ?? state.Power.BatteryConsumedMah,
                BatteryConsumedWh = observation.ConsumedWh ?? state.Power.BatteryConsumedWh,
                BatteryRemainingPercent = observation.RemainingPercent ?? state.Power.BatteryRemainingPercent,
                ObservedAt = observation.ObservedAt
            }
        };
    }

    /// <summary>
    /// Compatibility methods for gradual migration.
    /// </summary>
    /// <param name="observation"></param>
    public void ApplyPowerRail(VehiclePowerRailObservation observation)
    {
        state = state with { Power = state.Power with { ControllerVoltageVolts = observation.ControllerVoltageVolts, ServoVoltageVolts = observation.ServoVoltageVolts, StatusFlags = observation.Flags, ObservedAt = observation.ObservedAt } };
    }

    /// <summary>
    /// Compatibility methods for gradual migration.
    /// </summary>
    /// <param name="observation"></param>
    public void ApplyRadio(VehicleRadioObservation observation)
    {
        state = state with { Radio = new VehicleRadioState(observation.ChannelCount, observation.ChannelsRaw, observation.RssiPercent, observation.ObservedAt) };
    }

    /// <summary>
    /// Compatibility methods for gradual migration.
    /// </summary>
    /// <param name="observation"></param>
    public void ApplyNavigation(VehicleNavigationObservation observation)
    {
        state = state with
        {
            Navigation = state.Navigation with
            {
                DesiredRollDegrees = observation.DesiredRollDegrees,
                DesiredPitchDegrees = observation.DesiredPitchDegrees,
                NavigationBearingDegrees = observation.NavigationBearingDegrees,
                TargetBearingDegrees = observation.TargetBearingDegrees,
                WaypointDistanceMeters = observation.WaypointDistanceMeters,
                AltitudeErrorMeters = observation.AltitudeErrorMeters,
                AirspeedErrorMetersPerSecond = observation.AirspeedErrorMetersPerSecond,
                CrossTrackErrorMeters = observation.CrossTrackErrorMeters,
                ObservedAt = observation.ObservedAt
            }
        };
    }

    /// <summary>
    /// Compatibility methods for gradual migration.
    /// </summary>
    /// <param name="observation"></param>
    public void ApplyMissionProgress(VehicleMissionProgressObservation observation)
    {
        state = state with
        {
            Navigation = state.Navigation with
            {
                CurrentMissionSequence = observation.CurrentSequence,
                MissionItemCount = observation.Total,
                MissionState = observation.MissionState,
                MissionMode = observation.MissionMode,
                ObservedAt = observation.ObservedAt
            }
        };
    }

    /// <summary>
    /// Compatibility methods for gradual migration.
    /// </summary>
    /// <param name="observation"></param>
    public void ApplyEkf(VehicleEkfObservation observation)
    {
        state = state with
        {
            Health = new VehicleHealthState(
                observation.Flags,
                observation.IsHealthy,
                observation.VelocityVariance,
                observation.HorizontalPositionVariance,
                observation.VerticalPositionVariance,
                observation.CompassVariance,
                observation.TerrainAltitudeVariance,
                observation.AirspeedVariance,
                observation.ObservedAt)
        };
    }

    /// <summary>
    /// Compatibility methods for gradual migration.
    /// </summary>
    /// <param name="isArmed"></param>
    public void ApplyArm(bool isArmed)
    {
        state = state with { Flight = state.Flight with { IsArmed = isArmed } };
    }

    /// <summary>
    /// Compatibility methods for gradual migration.
    /// </summary>
    /// <param name="mode"></param>
    public void ApplyMode(VehicleMode mode)
    {
        state = state with { Flight = state.Flight with { Mode = mode } };
    }

    /// <summary>
    /// Compatibility methods for gradual migration.
    /// </summary>
    /// <param name="message"></param>
    public void ApplyStatusText(StatusTextMessage message)
    {
        Notifications.Add(new VehicleStatusText(message.SystemId, message.ComponentId, message.Text, message.ReceivedAt));
    }


    /// <summary>
    /// Compatibility methods for gradual migration.
    /// </summary>
    /// <param name="customMode"></param>
    /// <param name="vehicleType"></param>
    /// <param name="autopilot"></param>
    /// <param name="baseMode"></param>
    /// <param name="systemStatus"></param>
    /// <param name="mavLinkVersion"></param>
    /// <param name="receivedAt"></param>
    public void ApplyHeartbeat(uint customMode, byte vehicleType, byte autopilot, byte baseMode, byte systemStatus, byte mavLinkVersion, DateTimeOffset receivedAt)
    {
        ApplyHeartbeat(new VehicleHeartbeatObservation(customMode, vehicleType, autopilot, baseMode, systemStatus, mavLinkVersion, receivedAt));
    }

    /// <summary>
    /// Compatibility methods for gradual migration.
    /// </summary>
    /// <param name="latitude"></param>
    /// <param name="longitude"></param>
    /// <param name="altitude"></param>
    public void ApplyPosition(double latitude, double longitude, double altitude)
    {
        ApplyGlobalPosition(
            new VehicleGlobalPositionObservation(
                latitude,
                longitude,
                altitude,
                null,
                null,
                null,
                null,
                null,
                dateTimeProvider.UtcNow));
    }

    /// <summary>
    /// Compatibility methods for gradual migration.
    /// </summary>
    /// <param name="roll"></param>
    /// <param name="pitch"></param>
    /// <param name="yaw"></param>
    public void ApplyAttitude(double roll, double pitch, double yaw)
    {
        ApplyAttitude(
            new VehicleAttitudeObservation(
                roll,
                pitch,
                yaw,
                null,
                null,
                null,
                dateTimeProvider.UtcNow));
    }

    /// <summary>
    /// Compatibility methods for gradual migration.
    /// </summary>
    /// <param name="batteryRemaining"></param>
    /// <param name="batteryVoltage"></param>
    public void ApplyBattery(int? batteryRemaining, float? batteryVoltage)
    {
        ApplyBattery(new VehicleBatteryObservation(batteryVoltage, null, null, null, batteryRemaining, dateTimeProvider.UtcNow));
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
