namespace MissionPlanner.Core.Models;

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
