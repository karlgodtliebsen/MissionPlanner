using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Handlers.Abstractions;
using MissionPlanner.Core.Vehicles.Observations;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.MavLink.Messages;

namespace MissionPlanner.Core.Vehicles.Handlers;

/// <summary>
/// Provides the public API for PowerTelemetryHandler.
/// </summary>
public sealed class PowerTelemetryHandler(
    IVehicleRegistry vehicleRegistry,
    IDomainEventHub domainEventHub)
    : VehicleTelemetryHandlerBase(vehicleRegistry, domainEventHub), IVehicleMessageHandler
{
    /// <summary>
    /// Provides the public API for MessageTypes.
    /// </summary>
    public IReadOnlyCollection<Type> MessageTypes { get; } =
    [
        typeof(SysStatusMessage),
        typeof(BatteryStatusMessage),
        typeof(PowerStatusMessage)
    ];

    /// <summary>
    /// Provides the public API for HandleAsync.
    /// </summary>
    public async ValueTask HandleAsync(MavLinkMessage message, CancellationToken cancellationToken)
    {
        var vehicle = GetVehicle(message);
        if (vehicle is null)
        {
            return;
        }

        switch (message)
        {
            case SysStatusMessage status:
                vehicle.ApplyBattery(new VehicleBatteryObservation(
                    status.BatteryVoltage,
                    null,
                    null,
                    null,
                    status.BatteryRemaining,
                    status.ReceivedAt));
                break;

            case BatteryStatusMessage battery:
                vehicle.ApplyBattery(new VehicleBatteryObservation(
                    SumValidCellVoltages(battery.Voltages, battery.VoltagesExt),
                    battery.CurrentBattery < 0 ? null : battery.CurrentBattery / 100.0,
                    battery.CurrentConsumed < 0 ? null : battery.CurrentConsumed,
                    battery.EnergyConsumed < 0 ? null : battery.EnergyConsumed / 36.0,
                    battery.BatteryRemaining < 0 ? null : battery.BatteryRemaining,
                    battery.ReceivedAt));
                break;

            case PowerStatusMessage power:
                vehicle.ApplyPowerRail(new VehiclePowerRailObservation(
                    power.Vcc == ushort.MaxValue ? null : power.Vcc / 1000.0,
                    power.Vservo == ushort.MaxValue ? null : power.Vservo / 1000.0,
                    power.Flags,
                    power.ReceivedAt));
                break;
        }

        await PublishStateAsync(vehicle, cancellationToken).ConfigureAwait(false);
    }

    private static double? SumValidCellVoltages(
        IReadOnlyList<ushort> voltages,
        IReadOnlyList<ushort> extendedVoltages)
    {
        var valid = voltages.Concat(extendedVoltages)
            .Where(value => value is > 0 and < ushort.MaxValue)
            .ToArray();

        return valid.Length == 0 ? null : valid.Sum(value => value) / 1000.0;
    }
}
