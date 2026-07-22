namespace MissionPlanner.Core.Vehicles.Models;

/// <summary>
/// Provides the public API for VehicleMotionState.
/// </summary>
/// <param name="RollRadians">The RollRadians value.</param>
/// <param name="PitchRadians">The PitchRadians value.</param>
/// <param name="YawRadians">The YawRadians value.</param>
/// <param name="GroundSpeedMetersPerSecond">The GroundSpeedMetersPerSecond value.</param>
/// <param name="AirSpeedMetersPerSecond">The AirSpeedMetersPerSecond value.</param>
/// <param name="VerticalSpeedMetersPerSecond">The VerticalSpeedMetersPerSecond value.</param>
/// <param name="VelocityNorthMetersPerSecond">The VelocityNorthMetersPerSecond value.</param>
/// <param name="VelocityEastMetersPerSecond">The VelocityEastMetersPerSecond value.</param>
/// <param name="VelocityDownMetersPerSecond">The VelocityDownMetersPerSecond value.</param>
/// <param name="ObservedAt">The ObservedAt value.</param>
/// <param name="RollRateRadiansPerSecond">The roll angular rate in radians per second.</param>
/// <param name="PitchRateRadiansPerSecond">The pitch angular rate in radians per second.</param>
/// <param name="YawRateRadiansPerSecond">The yaw angular rate in radians per second.</param>
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
    DateTimeOffset? ObservedAt,
    double? RollRateRadiansPerSecond = null,
    double? PitchRateRadiansPerSecond = null,
    double? YawRateRadiansPerSecond = null)
{
    /// <summary>
    /// Provides the public API for Empty.
    /// </summary>
    public static VehicleMotionState Empty { get; } = new(null, null, null, null, null, null, null, null, null, null);

    /// <summary>Returns whether motion telemetry is older than <paramref name="maximumAge"/>.</summary>
    public bool IsStale(DateTimeOffset now, TimeSpan maximumAge) => ObservedAt is null || now - ObservedAt > maximumAge;
}
