namespace MissionPlanner.Core.Vehicles.Models;

/// <summary>
/// Provides the public API for VehicleDirectoryProgress.
/// </summary>
/// <param name="RemotePath">The remote path of the directory being loaded.</param>
public sealed record VehicleDirectoryProgress(string RemotePath);
