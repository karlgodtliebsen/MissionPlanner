namespace MissionPlanner.Core.Vehicles.Models;

/// <summary>Contains telemetry for a non-primary battery.</summary>
/// <param name="Id">The MAVLink battery instance identifier.</param>
/// <param name="VoltageVolts">The battery voltage in volts.</param>
/// <param name="CurrentAmps">The battery current in amperes.</param>
/// <param name="ConsumedMah">The consumed capacity in milliampere-hours.</param>
/// <param name="ConsumedWh">The consumed energy in watt-hours.</param>
/// <param name="RemainingPercent">The remaining charge percentage.</param>
/// <param name="ObservedAt">The reception timestamp.</param>
public sealed record VehicleBatteryState(byte Id, double? VoltageVolts, double? CurrentAmps, double? ConsumedMah, double? ConsumedWh, int? RemainingPercent, DateTimeOffset ObservedAt)
{
    /// <summary>Returns whether this battery state is older than <paramref name="maximumAge"/>.</summary>
    public bool IsStale(DateTimeOffset now, TimeSpan maximumAge) => now - ObservedAt > maximumAge;
}
