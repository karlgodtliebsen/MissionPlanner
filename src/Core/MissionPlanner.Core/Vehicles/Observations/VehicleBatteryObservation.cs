using MissionPlanner.Core.Vehicles.Abstractions;

namespace MissionPlanner.Core.Vehicles.Observations;

public sealed record VehicleBatteryObservation(double? VoltageVolts, double? CurrentAmps, double? ConsumedMah, double? ConsumedWh, int? RemainingPercent, DateTimeOffset ObservedAt) : IVehicleObservation;
