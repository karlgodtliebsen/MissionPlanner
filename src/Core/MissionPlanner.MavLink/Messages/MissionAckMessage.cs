using MissionPlanner.Transport;

namespace MissionPlanner.MavLink.Messages;

/// <summary>
/// Provides the public API for MissionAckMessage.
/// </summary>
public sealed record MissionAckMessage(byte SystemId, byte ComponentId, TransportEndPoint EndPoint, byte TargetSystem, byte TargetComponent, byte Result, byte MissionType, DateTimeOffset ReceivedAt, uint? OpaqueId = null)
    : MavLinkMessage(SystemId, ComponentId, MessageIds.MissionAck, EndPoint, ReceivedAt);
