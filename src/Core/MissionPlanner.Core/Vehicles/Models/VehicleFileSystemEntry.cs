namespace MissionPlanner.Core.Vehicles.Models;

/// <summary>
/// Provides the public API for VehicleFileSystemEntry.
/// </summary>
public sealed record VehicleFileSystemEntry(string Name, VehicleFileSystemEntryType Type, long? Size);
