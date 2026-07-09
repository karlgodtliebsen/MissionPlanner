using MissionPlanner.Transport;

namespace MissionPlanner.MavLink.Messages;

/// <summary>
/// Represents a MAVLink AHRS2 message.
/// </summary>
/// <param name="SystemId">The ID of the system that sent the message.</param>
/// <param name="ComponentId">The ID of the component that sent the message.</param>
/// <param name="EndPoint">The endpoint from which the message was received.</param>
/// <param name="Roll">The roll angle of the vehicle.</param>
/// <param name="Pitch">The pitch angle of the vehicle.</param>
/// <param name="Yaw">The yaw angle of the vehicle.</param>
/// <param name="Altitude">The altitude of the vehicle.</param>
/// <param name="Latitude">The latitude of the vehicle.</param>
/// <param name="Longitude">The longitude of the vehicle.</param>
/// <param name="ReceivedAt">The timestamp when the message was received.</param>
public sealed record Ahrs2Message(
    byte SystemId,
    byte ComponentId,
    TransportEndPoint EndPoint,
    float Roll,
    float Pitch,
    float Yaw,
    float Altitude,
    int Latitude,
    int Longitude,
    DateTimeOffset ReceivedAt)
    : MavLinkMessage(SystemId, ComponentId, MessageIds.Ahrs2, EndPoint, ReceivedAt);
