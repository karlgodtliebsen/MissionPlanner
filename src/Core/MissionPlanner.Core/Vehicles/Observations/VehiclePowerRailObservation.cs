using MissionPlanner.Core.Vehicles.Abstractions;

namespace MissionPlanner.Core.Vehicles.Observations;

/// <summary>
/// Provides the public API for VehiclePowerRailObservation.
/// </summary>
public sealed record VehiclePowerRailObservation(double? ControllerVoltageVolts, double? ServoVoltageVolts, ushort Flags, DateTimeOffset ObservedAt) : IVehicleObservation;
