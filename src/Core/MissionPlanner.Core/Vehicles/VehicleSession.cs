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

    /// <summary>
    /// Provides the public API for Id.
    /// </summary>
    public VehicleId Id => state.VehicleId;

    /// <summary>
    /// Provides the public API for State.
    /// </summary>
    public VehicleState State => state;

    /// <summary>
    /// Provides the public API for EndPoint.
    /// </summary>
    public TransportEndPoint EndPoint => endPoint;

    /// <summary>
    /// Provides the public API for Notifications.
    /// </summary>
    public IList<VehicleStatusText> Notifications { get; private set; } = [];

    /// <summary>
    /// Updates the connection state based on the last heartbeat timestamp and the provided thresholds for stale, degraded, and offline states.
    /// </summary>
    /// <param name="now"></param>
    /// <param name="staleAfter"></param>
    /// <param name="degradedAfter"></param>
    /// <param name="offlineAfter"></param>
    /// <returns></returns>
    public VehicleConnectionStateChanged? UpdateConnectionState(DateTimeOffset now, TimeSpan staleAfter, TimeSpan degradedAfter, TimeSpan offlineAfter)
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
        var identity = state.Identity;
        if (identity.VehicleType != observation.VehicleType || identity.Autopilot != observation.Autopilot || identity.MavLinkVersion != observation.MavLinkVersion)
        {
            var firmware = identity.Firmware with { Family = VehicleFirmwareIdentityFactory.MapFamily(observation.VehicleType, observation.Autopilot), MavType = observation.VehicleType, Autopilot = observation.Autopilot };
            identity = new VehicleIdentityState(observation.VehicleType, observation.Autopilot, observation.MavLinkVersion, firmware);
        }

        state = state with { Identity = identity, Flight = new VehicleFlightState(observation.CustomMode, observation.BaseMode, observation.SystemStatus, MapMode(observation.CustomMode), (observation.BaseMode & MavModeFlagSafetyArmed) != 0), Connection = new VehicleConnectionData(VehicleConnectionState.Online, observation.ObservedAt) };
    }

    /// <summary>
    /// Enriches the current connection identity with AUTOPILOT_VERSION data.
    /// </summary>
    /// <param name="observation">The decoded firmware observation.</param>
    public void ApplyFirmwareIdentity(VehicleFirmwareObservation observation)
    {
        var flightHash = ToOptionalHex(observation.FlightCustomVersion);
        var uid2 = ToOptionalHex(observation.Uid2);
        state = state with
        {
            Identity = state.Identity with
            {
                Firmware = state.Identity.Firmware with
                {
                    FlightVersion = FirmwareSemanticVersion.FromPacked(observation.FlightSoftwareVersion),
                    FlightGitHash = flightHash,
                    Capabilities = observation.Capabilities,
                    BoardVersion = observation.BoardVersion,
                    VendorId = observation.VendorId,
                    ProductId = observation.ProductId,
                    HardwareUid = observation.Uid == 0 ? null : observation.Uid,
                    HardwareUid2 = uid2
                }
            }
        };
    }

    private static string? ToOptionalHex(ReadOnlySpan<byte> bytes)
    {
        foreach (var value in bytes)
        {
            if (value != 0)
            {
                return Convert.ToHexString(bytes).ToLowerInvariant();
            }
        }

        return null;
    }

    /// <summary>
    /// Compatibility methods for gradual migration.
    /// </summary>
    /// <param name="observation"></param>
    public void ApplyAttitude(VehicleAttitudeObservation observation)
    {
        state = state with
        {
            Motion = state.Motion with
            {
                RollRadians = observation.RollRadians,
                PitchRadians = observation.PitchRadians,
                YawRadians = observation.YawRadians,
                RollRateRadiansPerSecond = observation.RollRateRadiansPerSecond,
                PitchRateRadiansPerSecond = observation.PitchRateRadiansPerSecond,
                YawRateRadiansPerSecond = observation.YawRateRadiansPerSecond,
                ObservedAt = observation.ObservedAt
            }
        };
    }

    /// <summary>Applies normalized VTOL and landed state.</summary>
    /// <param name="observation">The extended flight-state observation.</param>
    public void ApplyExtendedFlightState(VehicleExtendedFlightStateObservation observation)
    {
        state = state with { Flight = state.Flight with { VtolState = observation.VtolState, LandedState = observation.LandedState, ObservedAt = observation.ObservedAt } };
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
        return northMetersPerSecond is not { } north || eastMetersPerSecond is not { } east ? null : Math.Sqrt((north * north) + (east * east));
    }

    /// <summary>
    /// Applies the vehicle's home (launch) position from a HOME_POSITION message.
    /// </summary>
    /// <param name="observation">The home position observation.</param>
    public void ApplyHomePosition(VehicleHomePositionObservation observation)
    {
        state = state with { Position = state.Position with { HomeLatitudeDegrees = observation.LatitudeDegrees, HomeLongitudeDegrees = observation.LongitudeDegrees, HomeAltitudeMslMeters = observation.AltitudeMslMeters } };
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
        if (observation.ReceiverIndex > 0)
        {
            state = state with
            {
                Gps = state.Gps with
                {
                    SecondaryReceiver = new VehicleGpsReceiverState(
                        observation.FixType,
                        observation.SatellitesVisible,
                        observation.HorizontalDilution,
                        observation.VerticalDilution,
                        observation.GroundSpeedMetersPerSecond,
                        observation.CourseDegrees,
                        observation.HorizontalAccuracyMeters,
                        observation.VerticalAccuracyMeters,
                        observation.ObservedAt)
                }
            };
            return;
        }

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
            Estimator = state.Estimator with
            {
                RollRadians = observation.RollRadians,
                PitchRadians = observation.PitchRadians,
                YawRadians = observation.YawRadians,
                LatitudeDegrees = observation.LatitudeDegrees,
                LongitudeDegrees = observation.LongitudeDegrees,
                AltitudeMslMeters = observation.AltitudeMslMeters,
                Instance = 1,
                ObservedAt = observation.ObservedAt
            },
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
        if (observation.BatteryId > 0)
        {
            state = state with
            {
                Power = state.Power with
                {
                    SecondaryBattery = new VehicleBatteryState(
                        observation.BatteryId,
                        observation.VoltageVolts,
                        observation.CurrentAmps,
                        observation.ConsumedMah,
                        observation.ConsumedWh,
                        observation.RemainingPercent,
                        observation.ObservedAt)
                }
            };
            return;
        }

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
        var channels = state.Radio.ChannelsRaw.SequenceEqual(observation.ChannelsRaw)
            ? state.Radio.ChannelsRaw
            : observation.ChannelsRaw.ToArray();
        state = state with { Radio = state.Radio with { ChannelCount = observation.ChannelCount, ChannelsRaw = channels, RssiPercent = observation.RssiPercent, ObservedAt = observation.ObservedAt } };
    }

    /// <summary>Applies controller and onboard-sensor health.</summary>
    /// <param name="observation">The normalized system-health observation.</param>
    public void ApplySystemHealth(VehicleSystemHealthObservation observation)
    {
        state = state with
        {
            Health = state.Health with
            {
                SensorsPresent = observation.SensorsPresent,
                SensorsEnabled = observation.SensorsEnabled,
                SensorsHealthy = observation.SensorsHealthy,
                ControllerLoadPercent = observation.ControllerLoadPercent,
                CommunicationDropRatePercent = observation.CommunicationDropRatePercent,
                CommunicationErrors = observation.CommunicationErrors,
                SystemObservedAt = observation.ObservedAt
            }
        };
    }

    /// <summary>Applies estimator drift and error diagnostics.</summary>
    public void ApplyEstimatorDiagnostic(VehicleEstimatorDiagnosticObservation observation)
    {
        state = state with
        {
            Estimator = state.Estimator with
            {
                GyroDriftX = observation.GyroDriftX,
                GyroDriftY = observation.GyroDriftY,
                GyroDriftZ = observation.GyroDriftZ,
                RollPitchError = observation.RollPitchError,
                YawError = observation.YawError,
                ObservedAt = observation.ObservedAt
            }
        };
    }

    /// <summary>Applies an alternate estimator pose.</summary>
    public void ApplyEstimatorPose(VehicleEstimatorPoseObservation observation)
    {
        state = state with
        {
            Estimator = state.Estimator with
            {
                RollRadians = observation.RollRadians,
                PitchRadians = observation.PitchRadians,
                YawRadians = observation.YawRadians,
                LatitudeDegrees = observation.LatitudeDegrees,
                LongitudeDegrees = observation.LongitudeDegrees,
                AltitudeMslMeters = observation.AltitudeMslMeters,
                Instance = observation.Instance,
                ObservedAt = observation.ObservedAt
            }
        };
    }

    /// <summary>Applies vibration diagnostics.</summary>
    public void ApplyVibration(VehicleVibrationObservation observation)
    {
        var clipping = state.Vibration.Clipping.SequenceEqual(observation.Clipping) ? state.Vibration.Clipping : observation.Clipping.ToArray();
        state = state with { Vibration = new VehicleVibrationState(observation.X, observation.Y, observation.Z, clipping, observation.ObservedAt) };
    }

    /// <summary>Applies one pressure-sensor sample.</summary>
    public void ApplyPressure(VehiclePressureObservation observation)
    {
        var sample = new VehiclePressureSample(observation.Instance, observation.AbsoluteHectopascals, observation.DifferentialHectopascals, observation.TemperatureCelsius, observation.DifferentialTemperatureCelsius, observation.ObservedAt);
        state = state with { Pressure = observation.Instance switch { 0 => state.Pressure with { Primary = sample }, 1 => state.Pressure with { Secondary = sample }, var _ => state.Pressure with { Tertiary = sample } } };
    }

    /// <summary>Applies one keyed range-sensor sample.</summary>
    public void ApplyRange(VehicleRangeObservation observation)
    {
        var sample = new VehicleRangeSensorState(observation.Id, observation.DistanceMeters, observation.MinimumMeters, observation.MaximumMeters, observation.Orientation, observation.SignalQualityPercent, observation.ObservedAt);
        if (state.Range.Sensors.TryGetValue(observation.Id, out var current) && current == sample)
        {
            return;
        }

        var sensors = new Dictionary<byte, VehicleRangeSensorState>(state.Range.Sensors) { [observation.Id] = sample };
        state = state with { Range = new VehicleRangeState(sensors) };
    }

    /// <summary>Applies a wind-vector sample.</summary>
    public void ApplyWind(VehicleWindObservation observation)
    {
        state = state with
        {
            Environment = state.Environment with
            {
                WindNorthMetersPerSecond = observation.NorthMetersPerSecond,
                WindEastMetersPerSecond = observation.EastMetersPerSecond,
                WindDownMetersPerSecond = observation.DownMetersPerSecond,
                WindHorizontalVariance = observation.HorizontalVariance,
                WindVerticalVariance = observation.VerticalVariance,
                ObservedAt = observation.ObservedAt
            }
        };
    }

    /// <summary>Applies terrain height.</summary>
    public void ApplyTerrain(VehicleTerrainObservation observation)
    {
        state = state with { Environment = state.Environment with { TerrainHeightMslMeters = observation.TerrainHeightMslMeters, HeightAboveTerrainMeters = observation.HeightAboveTerrainMeters, ObservedAt = observation.ObservedAt } };
    }

    /// <summary>Applies altitude-source telemetry.</summary>
    public void ApplyAltitude(VehicleAltitudeObservation observation)
    {
        state = state with
        {
            Environment = state.Environment with
            {
                AltitudeMonotonicMeters = observation.MonotonicMeters,
                AltitudeMslMeters = observation.MslMeters,
                AltitudeLocalMeters = observation.LocalMeters,
                AltitudeRelativeMeters = observation.RelativeMeters,
                TerrainHeightMslMeters = observation.TerrainMeters ?? state.Environment.TerrainHeightMslMeters,
                BottomClearanceMeters = observation.BottomClearanceMeters,
                ObservedAt = observation.ObservedAt
            }
        };
    }

    /// <summary>Applies a vehicle system-clock sample.</summary>
    public void ApplyTime(VehicleTimeObservation observation)
    {
        state = state with { Time = new VehicleTimeState(observation.UnixTime, observation.BootTime, observation.ObservedAt) };
    }

    /// <summary>Applies telemetry-radio link statistics.</summary>
    /// <param name="observation">The normalized radio-link observation.</param>
    public void ApplyRadioLink(VehicleRadioLinkObservation observation)
    {
        state = state with
        {
            Radio = state.Radio with
            {
                LocalRssi = observation.LocalRssi,
                RemoteRssi = observation.RemoteRssi,
                TransmitBufferPercent = observation.TransmitBufferPercent,
                LocalNoise = observation.LocalNoise,
                RemoteNoise = observation.RemoteNoise,
                ReceiveErrors = observation.ReceiveErrors,
                CorrectedPackets = observation.CorrectedPackets,
                LinkObservedAt = observation.ObservedAt
            }
        };
    }

    /// <summary>Applies one bank of raw servo outputs.</summary>
    /// <param name="observation">The servo-output observation.</param>
    public void ApplyServoOutputs(VehicleServoOutputObservation observation)
    {
        var outputs = state.Radio.ServoOutputsRaw is { } current && current.SequenceEqual(observation.OutputsMicroseconds)
            ? current
            : observation.OutputsMicroseconds.ToArray();
        state = state with { Radio = state.Radio with { ServoOutputPort = observation.Port, ServoOutputsRaw = outputs, ServoObservedAt = observation.ObservedAt } };
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
    /// <param name="message">The complete or explicitly truncated status-text entry.</param>
    public void ApplyStatusText(VehicleStatusText message)
    {
        Notifications.Add(message);
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
