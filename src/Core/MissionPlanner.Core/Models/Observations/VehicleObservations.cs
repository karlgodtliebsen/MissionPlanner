namespace MissionPlanner.Core.Models.Observations;

public interface IVehicleObservation
{
    DateTimeOffset ObservedAt { get; }
}

public sealed record VehicleHeartbeatObservation(
    uint CustomMode,
    byte VehicleType,
    byte Autopilot,
    byte BaseMode,
    byte SystemStatus,
    byte MavLinkVersion,
    DateTimeOffset ObservedAt) : IVehicleObservation;

public sealed record VehicleAttitudeObservation(
    double RollRadians,
    double PitchRadians,
    double YawRadians,
    double? RollRateRadiansPerSecond,
    double? PitchRateRadiansPerSecond,
    double? YawRateRadiansPerSecond,
    DateTimeOffset ObservedAt) : IVehicleObservation;

public sealed record VehicleGlobalPositionObservation(
    double LatitudeDegrees,
    double LongitudeDegrees,
    double AltitudeMslMeters,
    double? RelativeAltitudeMeters,
    double? VelocityNorthMetersPerSecond,
    double? VelocityEastMetersPerSecond,
    double? VelocityDownMetersPerSecond,
    double? HeadingDegrees,
    DateTimeOffset ObservedAt) : IVehicleObservation;

public sealed record VehicleLocalPositionObservation(
    double NorthMeters,
    double EastMeters,
    double DownMeters,
    double VelocityNorthMetersPerSecond,
    double VelocityEastMetersPerSecond,
    double VelocityDownMetersPerSecond,
    DateTimeOffset ObservedAt) : IVehicleObservation;

public sealed record VehicleGpsObservation(
    GpsFixType FixType,
    int? SatellitesVisible,
    double? HorizontalDilution,
    double? VerticalDilution,
    double? GroundSpeedMetersPerSecond,
    double? CourseDegrees,
    double? HorizontalAccuracyMeters,
    double? VerticalAccuracyMeters,
    DateTimeOffset ObservedAt) : IVehicleObservation;

public sealed record VehicleHudObservation(
    double AirSpeedMetersPerSecond,
    double GroundSpeedMetersPerSecond,
    double HeadingDegrees,
    double AltitudeMslMeters,
    double VerticalSpeedMetersPerSecond,
    DateTimeOffset ObservedAt) : IVehicleObservation;

/// <summary>
/// Represents an AHRS-based observation of the vehicle's attitude and position.
/// This observation is typically used as a fallback when higher-priority telemetry
/// (such as ATTITUDE or GLOBAL_POSITION_INT) is unavailable.
/// </summary>
/// <param name="RollRadians">Vehicle roll in radians.</param>
/// <param name="PitchRadians">Vehicle pitch in radians.</param>
/// <param name="YawRadians">Vehicle yaw in radians.</param>
/// <param name="LatitudeDegrees">Latitude in decimal degrees.</param>
/// <param name="LongitudeDegrees">Longitude in decimal degrees.</param>
/// <param name="AltitudeMslMeters">Altitude above mean sea level in meters.</param>
/// <param name="IsEstimated"></param>
/// <param name="ObservedAt">Timestamp when the observation was received.</param>
public sealed record VehicleAhrsObservation(
    double RollRadians,
    double PitchRadians,
    double YawRadians,
    double LatitudeDegrees,
    double LongitudeDegrees,
    double AltitudeMslMeters,
    bool IsEstimated,
    DateTimeOffset ObservedAt) : IVehicleObservation;

public sealed record VehicleBatteryObservation(double? VoltageVolts, double? CurrentAmps, double? ConsumedMah, double? ConsumedWh, int? RemainingPercent, DateTimeOffset ObservedAt) : IVehicleObservation;

public sealed record VehiclePowerRailObservation(double? ControllerVoltageVolts, double? ServoVoltageVolts, ushort Flags, DateTimeOffset ObservedAt) : IVehicleObservation;

public sealed record VehicleRadioObservation(int ChannelCount, IReadOnlyList<ushort> ChannelsRaw, int? RssiPercent, DateTimeOffset ObservedAt) : IVehicleObservation;

public sealed record VehicleNavigationObservation(
    double DesiredRollDegrees,
    double DesiredPitchDegrees,
    double NavigationBearingDegrees,
    double TargetBearingDegrees,
    double WaypointDistanceMeters,
    double AltitudeErrorMeters,
    double AirspeedErrorMetersPerSecond,
    double CrossTrackErrorMeters,
    DateTimeOffset ObservedAt) : IVehicleObservation;

public sealed record VehicleMissionProgressObservation(
    ushort CurrentSequence,
    ushort? Total,
    byte? MissionState,
    byte? MissionMode,
    DateTimeOffset ObservedAt) : IVehicleObservation;

public sealed record VehicleEkfObservation(
    ushort Flags,
    bool IsHealthy,
    double VelocityVariance,
    double HorizontalPositionVariance,
    double VerticalPositionVariance,
    double CompassVariance,
    double TerrainAltitudeVariance,
    double? AirspeedVariance,
    DateTimeOffset ObservedAt) : IVehicleObservation;
