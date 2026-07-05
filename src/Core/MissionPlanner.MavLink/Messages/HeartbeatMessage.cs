using System.Net;

namespace MissionPlanner.MavLink.Messages;

/// <summary>
/// Represents a MAVLink Heartbeat message.
/// </summary>
/// <param name="SystemId">The ID of the system that sent the message.</param>
/// <param name="ComponentId">The ID of the component that sent the message.</param>
/// <param name="IPEndPoint">The IP endpoint from which the message was received.</param>
/// <param name="CustomMode">The custom mode of the vehicle.</param>
/// <param name="VehicleType">The type of the vehicle.</param>
/// <param name="Autopilot">The autopilot type of the vehicle.</param>
/// <param name="BaseMode">The base mode of the vehicle.</param>
/// <param name="SystemStatus">The system status of the vehicle.</param>
/// <param name="MavLinkVersion">The MAVLink version.</param>
/// <param name="ReceivedAt">The timestamp when the message was received.</param>
public sealed record HeartbeatMessage(byte SystemId, byte ComponentId, IPEndPoint IPEndPoint, uint CustomMode, byte VehicleType, byte Autopilot, byte BaseMode, byte SystemStatus, byte MavLinkVersion, DateTimeOffset ReceivedAt)
    : MavLinkMessage(SystemId, ComponentId, MessageIds.Heartbeat, IPEndPoint, ReceivedAt);