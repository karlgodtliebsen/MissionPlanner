using MissionPlanner.Core.Vehicles.Abstractions;

namespace MissionPlanner.Core.Vehicles.Observations;

public sealed record VehicleMissionProgressObservation(
    ushort CurrentSequence,
    ushort? Total,
    byte? MissionState,
    byte? MissionMode,
    DateTimeOffset ObservedAt) : IVehicleObservation;
