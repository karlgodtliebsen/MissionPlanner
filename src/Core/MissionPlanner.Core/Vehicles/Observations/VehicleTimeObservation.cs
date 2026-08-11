using MissionPlanner.Core.Vehicles.Abstractions;

namespace MissionPlanner.Core.Vehicles.Observations;

/// <summary>Represents a vehicle clock sample.</summary>
/// <param name="UnixTime">The vehicle UTC time.</param>
/// <param name="BootTime">The elapsed boot time.</param>
/// <param name="ObservedAt">The reception timestamp.</param>
public sealed record VehicleTimeObservation(DateTimeOffset? UnixTime, TimeSpan BootTime, DateTimeOffset ObservedAt) : IVehicleObservation;
