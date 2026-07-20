using MissionPlanner.Core.Vehicles.Abstractions;

namespace MissionPlanner.Core.Vehicles.Observations;

/// <summary>
/// Provides the public API for VehicleAttitudeObservation.
/// </summary>
/// <param name="RollRadians">The RollRadians value.</param>
/// <param name="PitchRadians">The PitchRadians value.</param>
/// <param name="YawRadians">The YawRadians value.</param>
/// <param name="RollRateRadiansPerSecond">The RollRateRadiansPerSecond value.</param>
/// <param name="PitchRateRadiansPerSecond">The PitchRateRadiansPerSecond value.</param>
/// <param name="YawRateRadiansPerSecond">The YawRateRadiansPerSecond value.</param>
/// <param name="ObservedAt">The ObservedAt value.</param>
public sealed record VehicleAttitudeObservation(
    double RollRadians,
    double PitchRadians,
    double YawRadians,
    double? RollRateRadiansPerSecond,
    double? PitchRateRadiansPerSecond,
    double? YawRateRadiansPerSecond,
    DateTimeOffset ObservedAt) : IVehicleObservation;
