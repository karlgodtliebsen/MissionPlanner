namespace MissionPlanner.Core.Vehicles.Models;

/// <summary>Contains a normalized barometer sample.</summary>
/// <param name="Instance">The zero-based barometer instance.</param>
/// <param name="AbsoluteHectopascals">The absolute pressure in hectopascals.</param>
/// <param name="DifferentialHectopascals">The differential pressure in hectopascals.</param>
/// <param name="TemperatureCelsius">The absolute-pressure sensor temperature in Celsius.</param>
/// <param name="DifferentialTemperatureCelsius">The differential-pressure sensor temperature in Celsius.</param>
/// <param name="ObservedAt">The reception timestamp.</param>
public sealed record VehiclePressureSample(int Instance, double AbsoluteHectopascals, double DifferentialHectopascals, double? TemperatureCelsius, double? DifferentialTemperatureCelsius, DateTimeOffset ObservedAt);

/// <summary>Contains the latest sample for each supported barometer instance.</summary>
/// <param name="Primary">The first barometer sample.</param>
/// <param name="Secondary">The second barometer sample.</param>
/// <param name="Tertiary">The third barometer sample.</param>
public sealed record VehiclePressureState(VehiclePressureSample? Primary, VehiclePressureSample? Secondary, VehiclePressureSample? Tertiary)
{
    /// <summary>Gets empty pressure state.</summary>
    public static VehiclePressureState Empty { get; } = new(null, null, null);
}
