using MissionPlanner.Transport;

namespace MissionPlanner.MavLink.Messages;

public sealed record MissionAckMessage(byte SystemId, byte ComponentId, TransportEndPoint EndPoint, byte TargetSystem, byte TargetComponent, byte Result, byte MissionType, DateTimeOffset ReceivedAt)
    : MavLinkMessage(SystemId, ComponentId, MessageIds.MissionAck, EndPoint, ReceivedAt);
