using MissionPlanner.Transport;

namespace MissionPlanner.MavLink.Messages;

/// <summary>
/// Represents a MAVLink StatusTextMessage, which contains status information from the drone.
/// </summary>
/// <param name="SystemId">The ID of the system that sent the message.</param>
/// <param name="ComponentId">The ID of the component that sent the message.</param>
/// <param name="EndPoint">The endpoint from which the message was received.</param>
/// <param name="Severity">The severity level of the status message.</param>
/// <param name="Text">The text of the status message.</param>
/// <param name="Id">The ID of the status message.</param>
/// <param name="ChunkSequence">The sequence number of the chunk if the message is split into multiple chunks.</param>
/// <param name="ReceivedAt">The timestamp when the message was received.</param>
/// <param name="IsTextTerminated">Whether the frame contains the final text chunk.</param>
public sealed record StatusTextMessage(
    byte SystemId,
    byte ComponentId,
    TransportEndPoint EndPoint,
    MavSeverity Severity,
    string Text,
    ushort? Id,
    byte? ChunkSequence,
    DateTimeOffset ReceivedAt,
    bool IsTextTerminated = true)
    : MavLinkMessage(SystemId, ComponentId, MessageIds.StatusText, EndPoint, ReceivedAt);
