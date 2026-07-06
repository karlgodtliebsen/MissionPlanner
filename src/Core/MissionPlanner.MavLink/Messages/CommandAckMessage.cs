using MissionPlanner.Transport;

namespace MissionPlanner.MavLink.Messages;

/// <summary>
/// 
/// </summary>
/// <param name="SystemId"></param>
/// <param name="ComponentId"></param>
/// <param name="EndPoint">The  endpoint from which the message was received.</param>
/// <param name="Command"></param>
/// <param name="Result"></param>
/// <param name="ReceivedAt"></param>
public sealed record CommandAckMessage(
    byte SystemId,
    byte ComponentId,
    TransportEndPoint EndPoint,
    ushort Command,
    byte Result,
    DateTimeOffset ReceivedAt)
    : MavLinkMessage(
        SystemId,
        ComponentId,
        MessageIds.CommandAck,
        EndPoint,
        ReceivedAt);
