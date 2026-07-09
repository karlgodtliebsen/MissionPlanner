using MissionPlanner.Transport;

namespace MissionPlanner.MavLink.Messages;

/// <summary>
/// Represents a MAVLink MissionCurrent message.
/// </summary>
/// <param name="SystemId">The ID of the system that sent the message.</param>
/// <param name="ComponentId">The ID of the component that sent the message.</param>
/// <param name="EndPoint">The endpoint from which the message was received.</param>
/// <param name="Sequence">The sequence number of the current mission item.</param>
/// <param name="Total">The total number of mission items.</param>
/// <param name="MissionState">The current state of the mission.</param>
/// <param name="MissionMode">The current mode of the mission.</param>
/// <param name="ReceivedAt">The timestamp when the message was received.</param>
public sealed record MissionCurrentMessage(
    byte SystemId,
    byte ComponentId,
    TransportEndPoint EndPoint,
    ushort Sequence,
    ushort Total,
    byte MissionState,
    byte MissionMode,
    DateTimeOffset ReceivedAt)
    : MavLinkMessage(SystemId, ComponentId, MessageIds.MissionCurrent, EndPoint, ReceivedAt);
