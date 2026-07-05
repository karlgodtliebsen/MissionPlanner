using System.Net;

namespace MissionPlanner.MavLink.Messages;

/// <summary>
/// 
/// </summary>
/// <param name="SystemId"></param>
/// <param name="ComponentId"></param>
/// <param name="IPEndPoint">The IP endpoint from which the message was received.</param>
/// <param name="Command"></param>
/// <param name="Result"></param>
/// <param name="ReceivedAt"></param>
public sealed record CommandAckMessage(
    byte SystemId,
    byte ComponentId,
    IPEndPoint IPEndPoint,
    ushort Command,
    byte Result,
    DateTimeOffset ReceivedAt)
    : MavLinkMessage(
        SystemId,
        ComponentId,
        MessageIds.CommandAck,
        IPEndPoint,
        ReceivedAt);