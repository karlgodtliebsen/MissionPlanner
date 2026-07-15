using MissionPlanner.Transport;

namespace MissionPlanner.MavLink.Messages;

public sealed record MissionCountMessage(byte SystemId, byte ComponentId, TransportEndPoint EndPoint, ushort Count, byte TargetSystem, byte TargetComponent, byte MissionType, DateTimeOffset ReceivedAt)
    : MavLinkMessage(SystemId, ComponentId, MessageIds.MissionCount, EndPoint, ReceivedAt);
