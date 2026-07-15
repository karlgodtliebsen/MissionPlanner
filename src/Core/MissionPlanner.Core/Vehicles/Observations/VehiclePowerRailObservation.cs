using MissionPlanner.Core.Vehicles.Abstractions;

namespace MissionPlanner.Core.Vehicles.Observations;

public sealed record VehiclePowerRailObservation(double? ControllerVoltageVolts, double? ServoVoltageVolts, ushort Flags, DateTimeOffset ObservedAt) : IVehicleObservation;
