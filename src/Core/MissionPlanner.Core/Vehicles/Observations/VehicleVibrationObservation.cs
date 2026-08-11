using MissionPlanner.Core.Vehicles.Abstractions;

namespace MissionPlanner.Core.Vehicles.Observations;

/// <summary>Represents normalized vibration diagnostics.</summary>
/// <param name="X">The X vibration metric.</param>
/// <param name="Y">The Y vibration metric.</param>
/// <param name="Z">The Z vibration metric.</param>
/// <param name="Clipping">The three IMU clipping counters.</param>
/// <param name="ObservedAt">The reception timestamp.</param>
public sealed record VehicleVibrationObservation(double X, double Y, double Z, IReadOnlyList<uint> Clipping, DateTimeOffset ObservedAt) : IVehicleObservation;
