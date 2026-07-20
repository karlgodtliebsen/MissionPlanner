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
    /// <summary>
    /// Provides the public API for Empty.
    /// </summary>
    public static VehicleMotionState Empty { get; } = new(null, null, null, null, null, null, null, null, null, null);
}
