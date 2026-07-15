namespace MissionPlanner.Core.Models.Observations;

public sealed record VehiclePowerRailObservation(double? ControllerVoltageVolts, double? ServoVoltageVolts, ushort Flags, DateTimeOffset ObservedAt) : IVehicleObservation;
