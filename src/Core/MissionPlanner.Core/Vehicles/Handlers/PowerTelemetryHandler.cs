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
        typeof(Battery2Message),
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
        var previous = vehicle.State;

        switch (message)
        {
            case SysStatusMessage status:
                vehicle.ApplyBattery(new VehicleBatteryObservation(
                    status.BatteryVoltage,
                    status.BatteryCurrent,
                    null,
                    null,
                    status.BatteryRemaining,
                    status.ReceivedAt));
                vehicle.ApplySystemHealth(new VehicleSystemHealthObservation(
                    status.SensorsPresent ?? 0,
                    status.SensorsEnabled ?? 0,
                    status.SensorsHealthy ?? 0,
                    status.ControllerLoadPercent ?? 0,
                    status.CommunicationDropRatePercent ?? 0,
                    status.CommunicationErrors ?? 0,
                    status.ReceivedAt));
                break;

            case BatteryStatusMessage battery:
                vehicle.ApplyBattery(new VehicleBatteryObservation(
                    SumValidCellVoltages(battery.Voltages, battery.VoltagesExt),
                    battery.CurrentBattery < 0 ? null : battery.CurrentBattery / 100.0,
                    battery.CurrentConsumed < 0 ? null : battery.CurrentConsumed,
                    battery.EnergyConsumed < 0 ? null : battery.EnergyConsumed / 36.0,
                    battery.BatteryRemaining < 0 ? null : battery.BatteryRemaining,
                    battery.ReceivedAt,
                    battery.Id));
                break;

            case Battery2Message battery2:
                vehicle.ApplyBattery(new VehicleBatteryObservation(
                    battery2.Voltage == ushort.MaxValue ? null : battery2.Voltage / 1000.0,
                    battery2.CurrentBattery < 0 ? null : battery2.CurrentBattery / 100.0,
                    null,
                    null,
                    null,
                    battery2.ReceivedAt,
                    1));
                break;

            case PowerStatusMessage power:
                vehicle.ApplyPowerRail(new VehiclePowerRailObservation(
                    power.Vcc == ushort.MaxValue ? null : power.Vcc / 1000.0,
                    power.Vservo == ushort.MaxValue ? null : power.Vservo / 1000.0,
                    power.Flags,
                    power.ReceivedAt));
                break;
        }

        await PublishStateIfChangedAsync(previous, vehicle, cancellationToken).ConfigureAwait(false);
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
