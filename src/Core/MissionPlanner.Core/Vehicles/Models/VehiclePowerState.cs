namespace MissionPlanner.Core.Models;

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
    public static VehiclePowerState Empty { get; } = new(null, null, null, null, null, null, null, null, null);
}
