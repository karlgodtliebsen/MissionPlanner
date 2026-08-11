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
