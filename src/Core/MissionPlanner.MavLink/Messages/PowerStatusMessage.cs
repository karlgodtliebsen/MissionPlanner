using MissionPlanner.Transport;

namespace MissionPlanner.MavLink.Messages;

/// <summary>
/// Represents a MAVLink PowerStatus message.   
/// </summary>
/// <param name="SystemId">The ID of the system that sent the message.</param>
/// <param name="ComponentId">The ID of the component that sent the message.</param>
/// <param name="EndPoint">The endpoint from which the message was received.</param>
/// <param name="Vcc">The voltage of the main power supply.</param>
/// <param name="Vservo">The voltage of the servo rail.</param>
/// <param name="Flags">The status flags.</param>
/// <param name="ReceivedAt">The timestamp when the message was received.</param>
public sealed record PowerStatusMessage(
    byte SystemId,
    byte ComponentId,
    TransportEndPoint EndPoint,
    ushort Vcc,
    ushort Vservo,
    ushort Flags,
    DateTimeOffset ReceivedAt)
    : MavLinkMessage(SystemId, ComponentId, MessageIds.PowerStatus, EndPoint, ReceivedAt);
