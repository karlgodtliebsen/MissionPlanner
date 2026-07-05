using System.Net;

namespace MissionPlanner.MavLink.Messages;

/// <summary>
/// Represents a MAVLink StatusTextMessage, which contains status information from the drone.
/// </summary>
/// <param name="SystemId">The ID of the system that sent the message.</param>
/// <param name="ComponentId">The ID of the component that sent the message.</param>
/// <param name="IPEndPoint">The IP endpoint from which the message was received.</param>
/// <param name="Severity">The severity level of the status message.</param>
/// <param name="Text">The text of the status message.</param>
/// <param name="Id">The ID of the status message.</param>
/// <param name="ChunkSequence">The sequence number of the chunk if the message is split into multiple chunks.</param>
/// <param name="ReceivedAt">The timestamp when the message was received.</param>
public sealed record StatusTextMessage(byte SystemId, byte ComponentId, IPEndPoint IPEndPoint, MavSeverity Severity, string Text, ushort? Id, byte? ChunkSequence, DateTimeOffset ReceivedAt)
    : MavLinkMessage(SystemId, ComponentId, MessageIds.StatusText, IPEndPoint, ReceivedAt);