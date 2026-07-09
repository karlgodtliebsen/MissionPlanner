using MissionPlanner.Transport;

namespace MissionPlanner.MavLink.Messages;

/// <summary>
/// Represents a MAVLink TimeSync message.
/// </summary>
/// <param name="SystemId">The ID of the system that sent the message.</param>
/// <param name="ComponentId">The ID of the component that sent the message.</param>
/// <param name="EndPoint">The endpoint from which the message was received.</param>
/// <param name="Tc1">The first timestamp.</param>
/// <param name="Ts1">The second timestamp.</param>
/// <param name="TargetSystem">The target system ID.</param>
/// <param name="TargetComponent">The target component ID.</param>
/// <param name="ReceivedAt">The timestamp when the message was received.</param>
public sealed record TimeSyncMessage(
    byte SystemId,
    byte ComponentId,
    TransportEndPoint EndPoint,
    long Tc1,
    long Ts1,
    byte TargetSystem,
    byte TargetComponent,
    DateTimeOffset ReceivedAt)
    : MavLinkMessage(SystemId, ComponentId, MessageIds.TimeSync, EndPoint, ReceivedAt);
