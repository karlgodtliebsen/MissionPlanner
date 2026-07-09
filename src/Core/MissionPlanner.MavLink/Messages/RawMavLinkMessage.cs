using MissionPlanner.Transport;

namespace MissionPlanner.MavLink.Messages;

/// <summary>
/// Represents a valid MAVLink frame whose specific payload type is not decoded yet.
/// </summary>
public sealed record RawMavLinkMessage(
    byte SystemId,
    byte ComponentId,
    TransportEndPoint EndPoint,
    uint RawMessageId,
    byte Sequence,
    byte[] Payload,
    byte[] RawFrame,
    DateTimeOffset ReceivedAt)
    : MavLinkMessage(SystemId, ComponentId, RawMessageId, EndPoint, ReceivedAt);
