using MissionPlanner.Transport;

namespace MissionPlanner.MavLink.Messages;

/// <summary>
/// Represents a MAVLink MemoryVect message.
/// </summary>
/// <param name="SystemId">The ID of the system that sent the message.</param>
/// <param name="ComponentId">The ID of the component that sent the message.</param>
/// <param name="EndPoint">The endpoint from which the message was received.</param>
/// <param name="Address">The memory address.</param>
/// <param name="Version">The version of the memory vector.</param>
/// <param name="Type">The encoded memory-value type.</param>
/// <param name="Values">The values of the memory vector.</param>
/// <param name="ReceivedAt">The timestamp when the message was received.</param>
public sealed record MemoryVectMessage(
    byte SystemId,
    byte ComponentId,
    TransportEndPoint EndPoint,
    ushort Address,
    byte Version,
    byte Type,
    IReadOnlyList<sbyte> Values,
    DateTimeOffset ReceivedAt)
    : MavLinkMessage(SystemId, ComponentId, MessageIds.MemoryVect, EndPoint, ReceivedAt);
