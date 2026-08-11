using MissionPlanner.Core.Vehicles.Abstractions;

namespace MissionPlanner.Core.Vehicles.Observations;

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
