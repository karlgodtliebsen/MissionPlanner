using MissionPlanner.Transport;

namespace MissionPlanner.MavLink.Messages;

/// <summary>
/// Represents a MAVLink BATTERY_STATUS message.
/// </summary>
public sealed record BatteryStatusMessage(
    byte SystemId,
    byte ComponentId,
    TransportEndPoint EndPoint,
    byte Id,
    byte BatteryFunction,
    byte BatteryType,
    short Temperature,
    IReadOnlyList<ushort> Voltages,
    short CurrentBattery,
    int CurrentConsumed,
    int EnergyConsumed,
    sbyte BatteryRemaining,
    int? TimeRemaining,
    byte? ChargeState,
    IReadOnlyList<ushort> VoltagesExt,
    byte? Mode,
    uint? FaultBitmask,
    DateTimeOffset ReceivedAt)
    : MavLinkMessage(SystemId, ComponentId, MessageIds.BatteryStatus, EndPoint, ReceivedAt);
