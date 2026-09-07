using MissionPlanner.Core.Vehicles.Abstractions;

namespace MissionPlanner.Core.Vehicles.Observations;

/// <summary>Reports actual satellites in use without refreshing position or fix freshness.</summary>
public sealed record VehicleGpsSatelliteUsageObservation(int? SatellitesUsed, DateTimeOffset ObservedAt) : IVehicleObservation;
