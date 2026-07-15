namespace MissionPlanner.Core.Models.Observations;

public sealed record VehicleBatteryObservation(double? VoltageVolts, double? CurrentAmps, double? ConsumedMah, double? ConsumedWh, int? RemainingPercent, DateTimeOffset ObservedAt) : IVehicleObservation;
