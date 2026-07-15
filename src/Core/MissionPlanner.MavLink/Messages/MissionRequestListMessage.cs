using MissionPlanner.Transport;

namespace MissionPlanner.MavLink.Messages;

public sealed record MissionRequestListMessage(byte SystemId, byte ComponentId, TransportEndPoint EndPoint, byte TargetSystem, byte TargetComponent, byte MissionType, DateTimeOffset ReceivedAt)
    : MavLinkMessage(SystemId, ComponentId, MessageIds.MissionRequestList, EndPoint, ReceivedAt);
