using MissionPlanner.Transport;

namespace MissionPlanner.MavLink.Messages;

/// <summary>
/// Represents the system status of the drone, including battery information.
/// </summary>
/// <param name="SystemId">The ID of the system sending the message.</param>
/// <param name="ComponentId">The ID of the component sending the message.</param>
/// <param name="EndPoint">The endpoint from which the message was received.</param>
/// <param name="BatteryRemaining">The remaining battery percentage.</param>
/// <param name="BatteryVoltage">The battery voltage in volts.</param>
/// <param name="ReceivedAt">The timestamp when the message was received.</param>
public sealed record SysStatusMessage(byte SystemId, byte ComponentId, TransportEndPoint EndPoint, int? BatteryRemaining, float? BatteryVoltage, DateTimeOffset ReceivedAt) : MavLinkMessage(
    SystemId, ComponentId, MessageIds.SysStatus, EndPoint, ReceivedAt);
