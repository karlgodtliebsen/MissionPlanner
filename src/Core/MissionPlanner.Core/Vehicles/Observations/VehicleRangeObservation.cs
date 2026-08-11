using MissionPlanner.Core.Vehicles.Abstractions;

namespace MissionPlanner.Core.Vehicles.Observations;

/// <summary>Represents one normalized range-sensor sample.</summary>
/// <param name="Id">The sensor ID.</param>
/// <param name="DistanceMeters">The measured distance in metres.</param>
/// <param name="MinimumMeters">The minimum range in metres.</param>
/// <param name="MaximumMeters">The maximum range in metres.</param>
/// <param name="Orientation">The sensor orientation.</param>
/// <param name="SignalQualityPercent">The signal quality percentage.</param>
/// <param name="ObservedAt">The reception timestamp.</param>
public sealed record VehicleRangeObservation(byte Id, double? DistanceMeters, double MinimumMeters, double MaximumMeters, byte Orientation, int? SignalQualityPercent, DateTimeOffset ObservedAt) : IVehicleObservation;
