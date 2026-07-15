namespace MissionPlanner.Core.Services;

/// <summary>
/// Represents the result of a vehicle registration operation.
/// </summary>
/// <param name="Vehicle">The registered vehicle session.</param>
public sealed record VehicleRegistryResult(VehicleSession Vehicle);