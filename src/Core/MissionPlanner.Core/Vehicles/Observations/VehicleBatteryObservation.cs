using MissionPlanner.Core.Vehicles.Abstractions;

namespace MissionPlanner.Core.Vehicles.Observations;

/// <summary>
/// Provides the public API for VehicleBatteryObservation.
/// </summary>
public sealed record VehicleBatteryObservation(double? VoltageVolts, double? CurrentAmps, double? ConsumedMah, double? ConsumedWh, int? RemainingPercent, DateTimeOffset ObservedAt) : IVehicleObservation;
