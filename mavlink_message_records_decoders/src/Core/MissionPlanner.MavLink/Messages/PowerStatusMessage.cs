using MissionPlanner.Transport;

namespace MissionPlanner.MavLink.Messages;

/// <summary>
/// Represents a MAVLink POWER_STATUS message.
/// </summary>
public sealed record PowerStatusMessage(
    byte SystemId,
    byte ComponentId,
    TransportEndPoint EndPoint,
    ushort Vcc,
    ushort Vservo,
    ushort Flags,
    DateTimeOffset ReceivedAt)
    : MavLinkMessage(SystemId, ComponentId, MessageIds.PowerStatus, EndPoint, ReceivedAt);
