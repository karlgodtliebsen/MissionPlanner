using MissionPlanner.Core.Vehicles.Abstractions;

namespace MissionPlanner.Core.Vehicles.Observations;

/// <summary>Represents estimator drift and error diagnostics.</summary>
/// <param name="GyroDriftX">The estimated X gyro drift.</param>
/// <param name="GyroDriftY">The estimated Y gyro drift.</param>
/// <param name="GyroDriftZ">The estimated Z gyro drift.</param>
/// <param name="RollPitchError">The roll/pitch error.</param>
/// <param name="YawError">The yaw error.</param>
/// <param name="ObservedAt">The reception timestamp.</param>
public sealed record VehicleEstimatorDiagnosticObservation(double GyroDriftX, double GyroDriftY, double GyroDriftZ, double RollPitchError, double YawError, DateTimeOffset ObservedAt) : IVehicleObservation;

/// <summary>Represents an alternate AHRS pose estimate.</summary>
/// <param name="Instance">The estimator instance.</param>
/// <param name="RollRadians">The roll in radians.</param>
/// <param name="PitchRadians">The pitch in radians.</param>
/// <param name="YawRadians">The yaw in radians.</param>
/// <param name="LatitudeDegrees">The latitude in degrees.</param>
/// <param name="LongitudeDegrees">The longitude in degrees.</param>
/// <param name="AltitudeMslMeters">The MSL altitude in metres.</param>
/// <param name="ObservedAt">The reception timestamp.</param>
public sealed record VehicleEstimatorPoseObservation(int Instance, double RollRadians, double PitchRadians, double YawRadians, double? LatitudeDegrees, double? LongitudeDegrees, double? AltitudeMslMeters, DateTimeOffset ObservedAt) : IVehicleObservation;

/// <summary>Represents normalized vibration diagnostics.</summary>
/// <param name="X">The X vibration metric.</param>
/// <param name="Y">The Y vibration metric.</param>
/// <param name="Z">The Z vibration metric.</param>
/// <param name="Clipping">The three IMU clipping counters.</param>
/// <param name="ObservedAt">The reception timestamp.</param>
public sealed record VehicleVibrationObservation(double X, double Y, double Z, IReadOnlyList<uint> Clipping, DateTimeOffset ObservedAt) : IVehicleObservation;

/// <summary>Represents one normalized pressure-sensor sample.</summary>
/// <param name="Instance">The zero-based sensor instance.</param>
/// <param name="AbsoluteHectopascals">The absolute pressure in hectopascals.</param>
/// <param name="DifferentialHectopascals">The differential pressure in hectopascals.</param>
/// <param name="TemperatureCelsius">The absolute sensor temperature in Celsius.</param>
/// <param name="DifferentialTemperatureCelsius">The differential sensor temperature in Celsius.</param>
/// <param name="ObservedAt">The reception timestamp.</param>
public sealed record VehiclePressureObservation(int Instance, double AbsoluteHectopascals, double DifferentialHectopascals, double? TemperatureCelsius, double? DifferentialTemperatureCelsius, DateTimeOffset ObservedAt) : IVehicleObservation;

/// <summary>Represents one normalized range-sensor sample.</summary>
/// <param name="Id">The sensor ID.</param>
/// <param name="DistanceMeters">The measured distance in metres.</param>
/// <param name="MinimumMeters">The minimum range in metres.</param>
/// <param name="MaximumMeters">The maximum range in metres.</param>
/// <param name="Orientation">The sensor orientation.</param>
/// <param name="SignalQualityPercent">The signal quality percentage.</param>
/// <param name="ObservedAt">The reception timestamp.</param>
public sealed record VehicleRangeObservation(byte Id, double? DistanceMeters, double MinimumMeters, double MaximumMeters, byte Orientation, int? SignalQualityPercent, DateTimeOffset ObservedAt) : IVehicleObservation;

/// <summary>Represents a normalized wind vector.</summary>
/// <param name="NorthMetersPerSecond">The north component.</param>
/// <param name="EastMetersPerSecond">The east component.</param>
/// <param name="DownMetersPerSecond">The down component.</param>
/// <param name="HorizontalVariance">The horizontal variance.</param>
/// <param name="VerticalVariance">The vertical variance.</param>
/// <param name="ObservedAt">The reception timestamp.</param>
public sealed record VehicleWindObservation(double? NorthMetersPerSecond, double? EastMetersPerSecond, double? DownMetersPerSecond, double? HorizontalVariance, double? VerticalVariance, DateTimeOffset ObservedAt) : IVehicleObservation;

/// <summary>Represents terrain height at the current vehicle location.</summary>
/// <param name="TerrainHeightMslMeters">The terrain height above MSL.</param>
/// <param name="HeightAboveTerrainMeters">The vehicle height above terrain.</param>
/// <param name="ObservedAt">The reception timestamp.</param>
public sealed record VehicleTerrainObservation(double TerrainHeightMslMeters, double HeightAboveTerrainMeters, DateTimeOffset ObservedAt) : IVehicleObservation;

/// <summary>Represents normalized altitude sources.</summary>
/// <param name="MonotonicMeters">The monotonic altitude.</param>
/// <param name="MslMeters">The MSL altitude.</param>
/// <param name="LocalMeters">The local altitude.</param>
/// <param name="RelativeMeters">The relative altitude.</param>
/// <param name="TerrainMeters">The terrain altitude.</param>
/// <param name="BottomClearanceMeters">The bottom clearance.</param>
/// <param name="ObservedAt">The reception timestamp.</param>
public sealed record VehicleAltitudeObservation(double? MonotonicMeters, double? MslMeters, double? LocalMeters, double? RelativeMeters, double? TerrainMeters, double? BottomClearanceMeters, DateTimeOffset ObservedAt) : IVehicleObservation;

/// <summary>Represents a vehicle clock sample.</summary>
/// <param name="UnixTime">The vehicle UTC time.</param>
/// <param name="BootTime">The elapsed boot time.</param>
/// <param name="ObservedAt">The reception timestamp.</param>
public sealed record VehicleTimeObservation(DateTimeOffset? UnixTime, TimeSpan BootTime, DateTimeOffset ObservedAt) : IVehicleObservation;
