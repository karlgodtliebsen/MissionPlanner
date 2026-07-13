namespace MissionPlanner.Core.Models.Observations;

public sealed record VehicleHeartbeatObservation(
    uint CustomMode,
    byte VehicleType,
    byte Autopilot,
    byte BaseMode,
    byte SystemStatus,
    byte MavLinkVersion,
    DateTimeOffset ObservedAt);

public sealed record VehicleAttitudeObservation(
    double RollRadians,
    double PitchRadians,
    double YawRadians,
    DateTimeOffset ObservedAt);

public sealed record VehicleGlobalPositionObservation(
    double LatitudeDegrees,
    double LongitudeDegrees,
    double AltitudeMslMeters,
    DateTimeOffset ObservedAt);

public sealed record VehicleLocalPositionObservation(
    double NorthMeters,
    double EastMeters,
    double DownMeters,
    double VelocityNorthMetersPerSecond,
    double VelocityEastMetersPerSecond,
    double VelocityDownMetersPerSecond,
    DateTimeOffset ObservedAt);

public sealed record VehicleGpsObservation(
    GpsFixType FixType,
    int? SatellitesVisible,
    double? HorizontalDilution,
    double? VerticalDilution,
    double? GroundSpeedMetersPerSecond,
    double? CourseDegrees,
    double? HorizontalAccuracyMeters,
    double? VerticalAccuracyMeters,
    DateTimeOffset ObservedAt);

public sealed record VehicleHudObservation(
    double AirSpeedMetersPerSecond,
    double GroundSpeedMetersPerSecond,
    double HeadingDegrees,
    double AltitudeMslMeters,
    double VerticalSpeedMetersPerSecond,
    DateTimeOffset ObservedAt);

public sealed record VehicleBatteryObservation(
    double? VoltageVolts,
    double? CurrentAmps,
    double? ConsumedMah,
    double? ConsumedWh,
    int? RemainingPercent,
    DateTimeOffset ObservedAt);

public sealed record VehiclePowerRailObservation(
    double? ControllerVoltageVolts,
    double? ServoVoltageVolts,
    ushort Flags,
    DateTimeOffset ObservedAt);

public sealed record VehicleRadioObservation(
    int ChannelCount,
    IReadOnlyList<ushort> ChannelsRaw,
    int? RssiPercent,
    DateTimeOffset ObservedAt);

public sealed record VehicleNavigationObservation(
    double DesiredRollDegrees,
    double DesiredPitchDegrees,
    double NavigationBearingDegrees,
    double TargetBearingDegrees,
    double WaypointDistanceMeters,
    double AltitudeErrorMeters,
    double AirspeedErrorMetersPerSecond,
    double CrossTrackErrorMeters,
    DateTimeOffset ObservedAt);

public sealed record VehicleMissionProgressObservation(
    ushort CurrentSequence,
    ushort? Total,
    byte? MissionState,
    byte? MissionMode,
    DateTimeOffset ObservedAt);

public sealed record VehicleEkfObservation(
    ushort Flags,
    bool IsHealthy,
    double VelocityVariance,
    double HorizontalPositionVariance,
    double VerticalPositionVariance,
    double CompassVariance,
    double TerrainAltitudeVariance,
    double? AirspeedVariance,
    DateTimeOffset ObservedAt);
