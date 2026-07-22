namespace MissionPlanner.Core.Vehicles.Models;

/// <summary>Contains estimator quality and alternate AHRS output.</summary>
/// <param name="GyroDriftX">The estimated gyro drift around X in radians per second.</param>
/// <param name="GyroDriftY">The estimated gyro drift around Y in radians per second.</param>
/// <param name="GyroDriftZ">The estimated gyro drift around Z in radians per second.</param>
/// <param name="RollPitchError">The roll/pitch estimate error.</param>
/// <param name="YawError">The yaw estimate error.</param>
/// <param name="RollRadians">The alternate estimator roll in radians.</param>
/// <param name="PitchRadians">The alternate estimator pitch in radians.</param>
/// <param name="YawRadians">The alternate estimator yaw in radians.</param>
/// <param name="LatitudeDegrees">The alternate estimator latitude in degrees.</param>
/// <param name="LongitudeDegrees">The alternate estimator longitude in degrees.</param>
/// <param name="AltitudeMslMeters">The alternate estimator MSL altitude in metres.</param>
/// <param name="Instance">The estimator instance number.</param>
/// <param name="ObservedAt">The reception timestamp.</param>
public sealed record VehicleEstimatorState(double? GyroDriftX, double? GyroDriftY, double? GyroDriftZ, double? RollPitchError, double? YawError, double? RollRadians, double? PitchRadians, double? YawRadians, double? LatitudeDegrees, double? LongitudeDegrees, double? AltitudeMslMeters, int? Instance, DateTimeOffset? ObservedAt)
{
    /// <summary>Gets empty estimator state.</summary>
    public static VehicleEstimatorState Empty { get; } = new(null, null, null, null, null, null, null, null, null, null, null, null, null);

    /// <summary>Returns whether estimator telemetry is stale.</summary>
    public bool IsStale(DateTimeOffset now, TimeSpan maximumAge) => ObservedAt is null || now - ObservedAt > maximumAge;
}
