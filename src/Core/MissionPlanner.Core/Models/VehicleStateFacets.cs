namespace MissionPlanner.Core.Models;

public sealed record VehicleIdentityState(byte VehicleType, byte Autopilot, byte MavLinkVersion);

public sealed record VehicleConnectionData(
    VehicleConnectionState State,
    DateTimeOffset LastHeartbeatAt);

public sealed record VehicleFlightState(
    uint CustomMode,
    byte BaseMode,
    byte SystemStatus,
    VehicleMode Mode,
    bool IsArmed);

public sealed record VehiclePositionState(
    double? LatitudeDegrees,
    double? LongitudeDegrees,
    double? AltitudeMslMeters,
    double? RelativeAltitudeMeters,
    double? LocalNorthMeters,
    double? LocalEastMeters,
    double? LocalDownMeters,
    double? HeadingDegrees,
    DateTimeOffset? ObservedAt)
{
    public static VehiclePositionState Empty { get; } = new(null, null, null, null, null, null, null, null, null);
}

public sealed record VehicleMotionState(
    double? RollRadians,
    double? PitchRadians,
    double? YawRadians,
    double? GroundSpeedMetersPerSecond,
    double? AirSpeedMetersPerSecond,
    double? VerticalSpeedMetersPerSecond,
    double? VelocityNorthMetersPerSecond,
    double? VelocityEastMetersPerSecond,
    double? VelocityDownMetersPerSecond,
    DateTimeOffset? ObservedAt)
{
    public static VehicleMotionState Empty { get; } = new(null, null, null, null, null, null, null, null, null, null);
}

public sealed record VehicleGpsState(
    GpsFixType FixType,
    int? SatellitesVisible,
    double? HorizontalDilution,
    double? VerticalDilution,
    double? GroundSpeedMetersPerSecond,
    double? CourseDegrees,
    double? HorizontalAccuracyMeters,
    double? VerticalAccuracyMeters,
    DateTimeOffset? ObservedAt)
{
    public static VehicleGpsState Empty { get; } = new(GpsFixType.Unknown, null, null, null, null, null, null, null, null);
}

public sealed record VehiclePowerState(
    double? BatteryVoltageVolts,
    double? BatteryCurrentAmps,
    double? BatteryConsumedMah,
    double? BatteryConsumedWh,
    int? BatteryRemainingPercent,
    double? ControllerVoltageVolts,
    double? ServoVoltageVolts,
    ushort? StatusFlags,
    DateTimeOffset? ObservedAt)
{
    public static VehiclePowerState Empty { get; } = new(null, null, null, null, null, null, null, null, null);
}

public sealed record VehicleRadioState(
    int? ChannelCount,
    IReadOnlyList<ushort> ChannelsRaw,
    int? RssiPercent,
    DateTimeOffset? ObservedAt)
{
    public static VehicleRadioState Empty { get; } = new(null, Array.Empty<ushort>(), null, null);
}

public sealed record VehicleNavigationState(
    double? DesiredRollDegrees,
    double? DesiredPitchDegrees,
    double? NavigationBearingDegrees,
    double? TargetBearingDegrees,
    double? WaypointDistanceMeters,
    double? AltitudeErrorMeters,
    double? AirspeedErrorMetersPerSecond,
    double? CrossTrackErrorMeters,
    ushort? CurrentMissionSequence,
    ushort? MissionItemCount,
    byte? MissionState,
    byte? MissionMode,
    DateTimeOffset? ObservedAt)
{
    public static VehicleNavigationState Empty { get; } = new(null, null, null, null, null, null, null, null, null, null, null, null, null);
}

public sealed record VehicleHealthState(
    ushort? EkfFlags,
    bool? EkfHealthy,
    double? VelocityVariance,
    double? HorizontalPositionVariance,
    double? VerticalPositionVariance,
    double? CompassVariance,
    double? TerrainAltitudeVariance,
    double? AirspeedVariance,
    DateTimeOffset? ObservedAt)
{
    public static VehicleHealthState Empty { get; } = new(null, null, null, null, null, null, null, null, null);
}

public enum GpsFixType : byte
{
    Unknown = 0,
    NoGps = 1,
    NoFix = 2,
    Fix2D = 3,
    Fix3D = 4,
    DifferentialGps = 5,
    RtkFloat = 6,
    RtkFixed = 7,
    Static = 8,
    Ppp = 9
}
