using MissionPlanner.Core.Vehicles.Abstractions;

namespace MissionPlanner.Core.Vehicles.Observations;

/// <summary>Represents terrain height at the current vehicle location.</summary>
/// <param name="TerrainHeightMslMeters">The terrain height above MSL.</param>
/// <param name="HeightAboveTerrainMeters">The vehicle height above terrain.</param>
/// <param name="ObservedAt">The reception timestamp.</param>
public sealed record VehicleTerrainObservation(double TerrainHeightMslMeters, double HeightAboveTerrainMeters, DateTimeOffset ObservedAt) : IVehicleObservation;
