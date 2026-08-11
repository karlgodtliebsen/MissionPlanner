using MissionPlanner.Core.Vehicles.Abstractions;

namespace MissionPlanner.Core.Vehicles.Observations;

/// <summary>Represents normalized altitude sources.</summary>
/// <param name="MonotonicMeters">The monotonic altitude.</param>
/// <param name="MslMeters">The MSL altitude.</param>
/// <param name="LocalMeters">The local altitude.</param>
/// <param name="RelativeMeters">The relative altitude.</param>
/// <param name="TerrainMeters">The terrain altitude.</param>
/// <param name="BottomClearanceMeters">The bottom clearance.</param>
/// <param name="ObservedAt">The reception timestamp.</param>
public sealed record VehicleAltitudeObservation(double? MonotonicMeters, double? MslMeters, double? LocalMeters, double? RelativeMeters, double? TerrainMeters, double? BottomClearanceMeters, DateTimeOffset ObservedAt) : IVehicleObservation;
