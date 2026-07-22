using MissionPlanner.Transport;

namespace MissionPlanner.MavLink.Messages;

/// <summary>
/// Provides the public API for FileTransferProtocolMessage.
/// </summary>
/// <param name="SystemId">The source MAVLink system identifier.</param>
/// <param name="ComponentId">The source MAVLink component identifier.</param>
/// <param name="EndPoint">The transport endpoint that supplied the message.</param>
/// <param name="TargetNetwork">The target network identifier.</param>
/// <param name="TargetSystem">The target system identifier.</param>
/// <param name="TargetComponent">The TargetComponent value.</param>
/// <param name="Payload">The Payload value.</param>
/// <param name="ReceivedAt">The time at which the message was received.</param>
public sealed record FileTransferProtocolMessage(
    byte SystemId, byte ComponentId, TransportEndPoint EndPoint,
    byte TargetNetwork, byte TargetSystem, byte TargetComponent,
    byte[] Payload, DateTimeOffset ReceivedAt)
    : MavLinkMessage(SystemId, ComponentId, MessageIds.FileTransferProtocol, EndPoint, ReceivedAt);
