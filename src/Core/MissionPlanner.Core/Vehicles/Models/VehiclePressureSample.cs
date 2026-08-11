namespace MissionPlanner.Core.Vehicles.Models;

/// <summary>Contains a normalized barometer sample.</summary>
/// <param name="Instance">The zero-based barometer instance.</param>
/// <param name="AbsoluteHectopascals">The absolute pressure in hectopascals.</param>
/// <param name="DifferentialHectopascals">The differential pressure in hectopascals.</param>
/// <param name="TemperatureCelsius">The absolute-pressure sensor temperature in Celsius.</param>
/// <param name="DifferentialTemperatureCelsius">The differential-pressure sensor temperature in Celsius.</param>
/// <param name="ObservedAt">The reception timestamp.</param>
public sealed record VehiclePressureSample(int Instance, double AbsoluteHectopascals, double DifferentialHectopascals, double? TemperatureCelsius, double? DifferentialTemperatureCelsius, DateTimeOffset ObservedAt);
