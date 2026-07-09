using MissionPlanner.Transport;

namespace MissionPlanner.MavLink.Messages;

/// <summary>
/// Represents an ArduPilot MAVLink AHRS2 message.
/// </summary>
public sealed record Ahrs2Message(
    byte SystemId,
    byte ComponentId,
    TransportEndPoint EndPoint,
    float Roll,
    float Pitch,
    float Yaw,
    float Altitude,
    double Latitude,
    double Longitude,
    DateTimeOffset ReceivedAt)
    : MavLinkMessage(SystemId, ComponentId, MessageIds.Ahrs2, EndPoint, ReceivedAt);
