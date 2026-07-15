namespace MissionPlanner.Core.Models.Observations;

public sealed record VehicleMissionProgressObservation(
    ushort CurrentSequence,
    ushort? Total,
    byte? MissionState,
    byte? MissionMode,
    DateTimeOffset ObservedAt) : IVehicleObservation;
