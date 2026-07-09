using MissionPlanner.Transport;

namespace MissionPlanner.MavLink.Messages;

/// <summary>
/// Represents a MAVLink TIMESYNC message.
/// </summary>
public sealed record TimeSyncMessage(
    byte SystemId,
    byte ComponentId,
    TransportEndPoint EndPoint,
    long Tc1,
    long Ts1,
    DateTimeOffset ReceivedAt)
    : MavLinkMessage(SystemId, ComponentId, MessageIds.TimeSync, EndPoint, ReceivedAt);
