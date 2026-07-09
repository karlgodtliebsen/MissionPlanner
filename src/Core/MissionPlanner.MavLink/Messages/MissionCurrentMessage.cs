using MissionPlanner.Transport;

namespace MissionPlanner.MavLink.Messages;

/// <summary>
/// Represents a MAVLink MISSION_CURRENT message.
/// </summary>
public sealed record MissionCurrentMessage(
    byte SystemId,
    byte ComponentId,
    TransportEndPoint EndPoint,
    ushort Sequence,
    ushort? Total,
    byte? MissionState,
    byte? MissionMode,
    DateTimeOffset ReceivedAt)
    : MavLinkMessage(SystemId, ComponentId, MessageIds.MissionCurrent, EndPoint, ReceivedAt);
