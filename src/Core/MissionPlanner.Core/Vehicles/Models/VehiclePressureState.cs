namespace MissionPlanner.Core.Vehicles.Models;

/// <summary>Contains the latest sample for each supported barometer instance.</summary>
/// <param name="Primary">The first barometer sample.</param>
/// <param name="Secondary">The second barometer sample.</param>
/// <param name="Tertiary">The third barometer sample.</param>
public sealed record VehiclePressureState(VehiclePressureSample? Primary, VehiclePressureSample? Secondary, VehiclePressureSample? Tertiary)
{
    /// <summary>Gets empty pressure state.</summary>
    public static VehiclePressureState Empty { get; } = new(null, null, null);
}
