using MissionPlanner.Core.DomainEvents;
using MissionPlanner.Core.Models;
using MissionPlanner.Core.Models.Observations;
using MissionPlanner.MavLink.Messages;
using MissionPlanner.Transport;

namespace MissionPlanner.Core.Services;

public class VehicleSession(VehicleState initialState, TransportEndPoint endPoint)
{
    private const byte MavModeFlagSafetyArmed = 0b1000_0000;
    private VehicleState state = initialState;

    public VehicleId Id => state.VehicleId;
    public VehicleState State => state;
    public TransportEndPoint EndPoint => endPoint;
    public IList<VehicleStatusText> Notifications { get; private set; } = [];

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

    public void ApplyHeartbeat(VehicleHeartbeatObservation observation)
    {
        state = state with
        {
            Identity = new VehicleIdentityState(
                observation.VehicleType,
                observation.Autopilot,
                observation.MavLinkVersion),
            Flight = new VehicleFlightState(
                observation.CustomMode,
                observation.BaseMode,
                observation.SystemStatus,
                MapMode(observation.CustomMode),
                (observation.BaseMode & MavModeFlagSafetyArmed) != 0),
            Connection = new VehicleConnectionData(
                VehicleConnectionState.Online,
                observation.ObservedAt)
        };
    }

    public void ApplyAttitude(VehicleAttitudeObservation observation)
    {
        state = state with
        {
            Motion = state.Motion with
            {
                RollRadians = observation.RollRadians,
                PitchRadians = observation.PitchRadians,
                YawRadians = observation.YawRadians,
                ObservedAt = observation.ObservedAt
            }
        };
    }

    public void ApplyGlobalPosition(VehicleGlobalPositionObservation observation)
    {
        state = state with
        {
            Position = state.Position with
            {
                LatitudeDegrees = observation.LatitudeDegrees,
                LongitudeDegrees = observation.LongitudeDegrees,
                AltitudeMslMeters = observation.AltitudeMslMeters,
                ObservedAt = observation.ObservedAt
            }
        };
    }

    public void ApplyLocalPosition(VehicleLocalPositionObservation observation)
    {
        state = state with
        {
            Position = state.Position with
            {
                LocalNorthMeters = observation.NorthMeters,
                LocalEastMeters = observation.EastMeters,
                LocalDownMeters = observation.DownMeters,
                ObservedAt = observation.ObservedAt
            },
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
            Position = state.Position with
            {
                HeadingDegrees = observation.CourseDegrees ?? state.Position.HeadingDegrees,
                ObservedAt = observation.ObservedAt
            }
        };
    }

    public void ApplyHud(VehicleHudObservation observation)
    {
        state = state with
        {
            Motion = state.Motion with
            {
                AirSpeedMetersPerSecond = observation.AirSpeedMetersPerSecond,
                GroundSpeedMetersPerSecond = observation.GroundSpeedMetersPerSecond,
                VerticalSpeedMetersPerSecond = observation.VerticalSpeedMetersPerSecond,
                ObservedAt = observation.ObservedAt
            },
            Position = state.Position with
            {
                AltitudeMslMeters = observation.AltitudeMslMeters,
                HeadingDegrees = observation.HeadingDegrees,
                ObservedAt = observation.ObservedAt
            }
        };
    }

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

    public void ApplyPowerRail(VehiclePowerRailObservation observation)
    {
        state = state with
        {
            Power = state.Power with
            {
                ControllerVoltageVolts = observation.ControllerVoltageVolts,
                ServoVoltageVolts = observation.ServoVoltageVolts,
                StatusFlags = observation.Flags,
                ObservedAt = observation.ObservedAt
            }
        };
    }

    public void ApplyRadio(VehicleRadioObservation observation)
    {
        state = state with
        {
            Radio = new VehicleRadioState(
                observation.ChannelCount,
                observation.ChannelsRaw,
                observation.RssiPercent,
                observation.ObservedAt)
        };
    }

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

    public void ApplyArm(bool isArmed) =>
        state = state with { Flight = state.Flight with { IsArmed = isArmed } };

    public void ApplyMode(VehicleMode mode) =>
        state = state with { Flight = state.Flight with { Mode = mode } };

    public void ApplyStatusText(StatusTextMessage message) =>
        Notifications.Add(new VehicleStatusText(message.SystemId, message.ComponentId, message.Text, message.ReceivedAt));

    // Compatibility methods for gradual migration.
    public void ApplyHeartbeat(uint customMode, byte vehicleType, byte autopilot, byte baseMode, byte systemStatus, byte mavLinkVersion, DateTimeOffset receivedAt) =>
        ApplyHeartbeat(new VehicleHeartbeatObservation(customMode, vehicleType, autopilot, baseMode, systemStatus, mavLinkVersion, receivedAt));

    public void ApplyPosition(double latitude, double longitude, double altitude) =>
        ApplyGlobalPosition(new VehicleGlobalPositionObservation(latitude, longitude, altitude, DateTimeOffset.UtcNow));

    public void ApplyAttitude(double roll, double pitch, double yaw) =>
        ApplyAttitude(new VehicleAttitudeObservation(roll, pitch, yaw, DateTimeOffset.UtcNow));

    public void ApplyBattery(int? batteryRemaining, float? batteryVoltage) =>
        ApplyBattery(new VehicleBatteryObservation(batteryVoltage, null, null, null, batteryRemaining, DateTimeOffset.UtcNow));

    private static VehicleMode MapMode(uint customMode) => customMode switch
    {
        0 => VehicleMode.Stabilize,
        2 => VehicleMode.AltHold,
        4 => VehicleMode.Guided,
        5 => VehicleMode.Loiter,
        6 => VehicleMode.Rtl,
        9 => VehicleMode.Land,
        _ => VehicleMode.Unknown
    };
}
