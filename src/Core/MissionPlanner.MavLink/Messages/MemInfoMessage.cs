using MissionPlanner.Transport;

namespace MissionPlanner.MavLink.Messages;

/// <summary>
/// Represents an ArduPilot MAVLink MEMINFO message.
/// </summary>
public sealed record MemInfoMessage(
    byte SystemId,
    byte ComponentId,
    TransportEndPoint EndPoint,
    ushort BrkVal,
    ushort FreeMem,
    uint? FreeMem32,
    DateTimeOffset ReceivedAt)
    : MavLinkMessage(SystemId, ComponentId, MessageIds.MemInfo, EndPoint, ReceivedAt);
