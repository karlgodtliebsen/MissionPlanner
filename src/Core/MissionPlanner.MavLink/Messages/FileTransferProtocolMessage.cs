using MissionPlanner.Transport;

namespace MissionPlanner.MavLink.Messages;

/// <summary>
/// Provides the public API for FileTransferProtocolMessage.
/// </summary>
/// <param name="TargetComponent">The TargetComponent value.</param>
/// <param name="Payload">The Payload value.</param>
public sealed record FileTransferProtocolMessage(
    byte SystemId, byte ComponentId, TransportEndPoint EndPoint,
    byte TargetNetwork, byte TargetSystem, byte TargetComponent,
    byte[] Payload, DateTimeOffset ReceivedAt)
    : MavLinkMessage(SystemId, ComponentId, MessageIds.FileTransferProtocol, EndPoint, ReceivedAt);
