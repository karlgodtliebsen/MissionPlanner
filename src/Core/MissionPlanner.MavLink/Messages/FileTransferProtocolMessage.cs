using MissionPlanner.Transport;

namespace MissionPlanner.MavLink.Messages;

public sealed record FileTransferProtocolMessage(
    byte SystemId, byte ComponentId, TransportEndPoint EndPoint,
    byte TargetNetwork, byte TargetSystem, byte TargetComponent,
    byte[] Payload, DateTimeOffset ReceivedAt)
    : MavLinkMessage(SystemId, ComponentId, MessageIds.FileTransferProtocol, EndPoint, ReceivedAt);
