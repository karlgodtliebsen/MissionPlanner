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
/// <param name="Progress">Command progress from zero to one hundred, or 255 when not supplied.</param>
/// <param name="ResultParameter2">Command-specific result detail.</param>
/// <param name="TargetSystemId">The acknowledged target system when supplied by MAVLink 2.</param>
/// <param name="TargetComponentId">The acknowledged target component when supplied by MAVLink 2.</param>
public sealed record CommandAckMessage(
    byte SystemId,
    byte ComponentId,
    TransportEndPoint EndPoint,
    ushort Command,
    byte Result,
    DateTimeOffset ReceivedAt,
    byte Progress = byte.MaxValue,
    int ResultParameter2 = 0,
    byte? TargetSystemId = null,
    byte? TargetComponentId = null)
    : MavLinkMessage(
        SystemId,
        ComponentId,
        MessageIds.CommandAck,
        EndPoint,
        ReceivedAt);
