using MissionPlanner.Core.Vehicles.Abstractions;

namespace MissionPlanner.Core.Vehicles.Observations;

/// <summary>
/// Provides the public API for VehicleMissionProgressObservation.
/// </summary>
/// <param name="CurrentSequence">The CurrentSequence value.</param>
/// <param name="Total">The Total value.</param>
/// <param name="MissionState">The MissionState value.</param>
/// <param name="MissionMode">The MissionMode value.</param>
/// <param name="ObservedAt">The ObservedAt value.</param>
public sealed record VehicleMissionProgressObservation(
    ushort CurrentSequence,
    ushort? Total,
    byte? MissionState,
    byte? MissionMode,
    DateTimeOffset ObservedAt) : IVehicleObservation;
