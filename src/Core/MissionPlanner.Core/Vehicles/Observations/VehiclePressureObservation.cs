using MissionPlanner.Core.Vehicles.Abstractions;

namespace MissionPlanner.Core.Vehicles.Observations;

/// <summary>Represents one normalized pressure-sensor sample.</summary>
/// <param name="Instance">The zero-based sensor instance.</param>
/// <param name="AbsoluteHectopascals">The absolute pressure in hectopascals.</param>
/// <param name="DifferentialHectopascals">The differential pressure in hectopascals.</param>
/// <param name="TemperatureCelsius">The absolute sensor temperature in Celsius.</param>
/// <param name="DifferentialTemperatureCelsius">The differential sensor temperature in Celsius.</param>
/// <param name="ObservedAt">The reception timestamp.</param>
public sealed record VehiclePressureObservation(int Instance, double AbsoluteHectopascals, double DifferentialHectopascals, double? TemperatureCelsius, double? DifferentialTemperatureCelsius, DateTimeOffset ObservedAt) : IVehicleObservation;
