using MissionPlanner.Transport;

namespace MissionPlanner.MavLink.Messages;

/// <summary>
/// Represents a generic MAVLink message.
/// </summary>
/// <param name="SystemId">The ID of the system that sent the message.</param>
/// <param name="ComponentId">The ID of the component that sent the message.</param>
/// <param name="MessageId">The ID of the message.</param>
/// <param name="EndPoint">The endpoint from which the message was received.</param>
/// <param name="ReceivedAt">The timestamp when the message was received.</param>
public abstract record MavLinkMessage(byte SystemId, byte ComponentId, uint MessageId, TransportEndPoint EndPoint, DateTimeOffset ReceivedAt);
