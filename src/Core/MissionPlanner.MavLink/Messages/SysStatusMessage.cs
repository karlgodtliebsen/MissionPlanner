using System.Net;

namespace MissionPlanner.MavLink.Messages;

/// <summary>
/// Represents the system status of the drone, including battery information.
/// </summary>
/// <param name="SystemId">The ID of the system sending the message.</param>
/// <param name="ComponentId">The ID of the component sending the message.</param>
/// <param name="IPEndPoint">The IP endpoint from which the message was received.</param>
/// <param name="BatteryRemaining">The remaining battery percentage.</param>
/// <param name="BatteryVoltage">The battery voltage in volts.</param>
/// <param name="ReceivedAt">The timestamp when the message was received.</param>
public sealed record SysStatusMessage(byte SystemId, byte ComponentId, IPEndPoint IPEndPoint, int? BatteryRemaining, float? BatteryVoltage, DateTimeOffset ReceivedAt) : MavLinkMessage(
    SystemId, ComponentId, MessageIds.SysStatus, IPEndPoint, ReceivedAt);