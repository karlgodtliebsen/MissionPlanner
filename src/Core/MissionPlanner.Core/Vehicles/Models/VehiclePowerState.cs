namespace MissionPlanner.Core.Vehicles.Models;

/// <summary>
/// Provides the public API for VehiclePowerState.
/// </summary>
/// <param name="BatteryVoltageVolts">The BatteryVoltageVolts value.</param>
/// <param name="BatteryCurrentAmps">The BatteryCurrentAmps value.</param>
/// <param name="BatteryConsumedMah">The BatteryConsumedMah value.</param>
/// <param name="BatteryConsumedWh">The BatteryConsumedWh value.</param>
/// <param name="BatteryRemainingPercent">The BatteryRemainingPercent value.</param>
/// <param name="ControllerVoltageVolts">The ControllerVoltageVolts value.</param>
/// <param name="ServoVoltageVolts">The ServoVoltageVolts value.</param>
/// <param name="StatusFlags">The StatusFlags value.</param>
/// <param name="ObservedAt">The ObservedAt value.</param>
public sealed record VehiclePowerState(
    double? BatteryVoltageVolts,
    double? BatteryCurrentAmps,
    double? BatteryConsumedMah,
    double? BatteryConsumedWh,
    int? BatteryRemainingPercent,
    double? ControllerVoltageVolts,
    double? ServoVoltageVolts,
    ushort? StatusFlags,
    DateTimeOffset? ObservedAt)
{
    /// <summary>
    /// Provides the public API for Empty.
    /// </summary>
    public static VehiclePowerState Empty { get; } = new(null, null, null, null, null, null, null, null, null);
}
