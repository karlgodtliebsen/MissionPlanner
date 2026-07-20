using MissionPlanner.Transport;

namespace MissionPlanner.MavLink.Messages;

/// <summary>
/// Provides the public API for MissionItemReachedMessage.
/// </summary>
public sealed record MissionItemReachedMessage(byte SystemId, byte ComponentId, TransportEndPoint EndPoint, ushort Sequence, DateTimeOffset ReceivedAt)
    : MavLinkMessage(SystemId, ComponentId, MessageIds.MissionItemReached, EndPoint, ReceivedAt);
