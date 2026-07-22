using MissionPlanner.Core.Vehicles.Abstractions;

namespace MissionPlanner.Core.Vehicles.Observations;

/// <summary>Represents normalized telemetry for one battery instance.</summary>
/// <param name="VoltageVolts">The voltage in volts.</param>
/// <param name="CurrentAmps">The current in amperes.</param>
/// <param name="ConsumedMah">The consumed capacity in milliampere-hours.</param>
/// <param name="ConsumedWh">The consumed energy in watt-hours.</param>
/// <param name="RemainingPercent">The remaining charge percentage.</param>
/// <param name="ObservedAt">The reception timestamp.</param>
/// <param name="BatteryId">The MAVLink battery instance identifier.</param>
public sealed record VehicleBatteryObservation(double? VoltageVolts, double? CurrentAmps, double? ConsumedMah, double? ConsumedWh, int? RemainingPercent, DateTimeOffset ObservedAt, byte BatteryId = 0) : IVehicleObservation;
