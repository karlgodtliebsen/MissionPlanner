using MissionPlanner.Transport;

namespace MissionPlanner.MavLink.Messages;

public sealed record MissionRequestIntMessage(byte SystemId, byte ComponentId, TransportEndPoint EndPoint, ushort Sequence, byte TargetSystem, byte TargetComponent, byte MissionType, DateTimeOffset ReceivedAt)
    : MavLinkMessage(SystemId, ComponentId, MessageIds.MissionRequestInt, EndPoint, ReceivedAt);
