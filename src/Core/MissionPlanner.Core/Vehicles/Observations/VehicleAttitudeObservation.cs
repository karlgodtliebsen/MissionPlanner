using MissionPlanner.Core.Vehicles.Abstractions;

namespace MissionPlanner.Core.Vehicles.Observations;

public sealed record VehicleAttitudeObservation(
    double RollRadians,
    double PitchRadians,
    double YawRadians,
    double? RollRateRadiansPerSecond,
    double? PitchRateRadiansPerSecond,
    double? YawRateRadiansPerSecond,
    DateTimeOffset ObservedAt) : IVehicleObservation;
