using MissionPlanner.Transport;

namespace MissionPlanner.MavLink.Messages;

/// <summary>
/// Represents a MAVLink LOCAL_POSITION_NED message.
/// </summary>
public sealed record LocalPositionNedMessage(
    byte SystemId,
    byte ComponentId,
    TransportEndPoint EndPoint,
    uint TimeBootMs,
    float X,
    float Y,
    float Z,
    float Vx,
    float Vy,
    float Vz,
    DateTimeOffset ReceivedAt)
    : MavLinkMessage(SystemId, ComponentId, MessageIds.LocalPositionNed, EndPoint, ReceivedAt);
