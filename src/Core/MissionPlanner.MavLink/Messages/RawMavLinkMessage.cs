using MissionPlanner.Transport;

namespace MissionPlanner.MavLink.Messages;

/// <summary>
/// Represents a CRC-valid MAVLink frame whose payload has no registered typed decoder.
/// </summary>
/// <param name="MavLinkVersion">The wire protocol version inferred from the frame marker.</param>
/// <param name="SystemId">The source system identifier.</param>
/// <param name="ComponentId">The source component identifier.</param>
/// <param name="EndPoint">The source transport endpoint.</param>
/// <param name="RawMessageId">The actual wire message identifier.</param>
/// <param name="Sequence">The packet sequence number.</param>
/// <param name="IncompatibilityFlags">The MAVLink 2 incompatibility flags.</param>
/// <param name="CompatibilityFlags">The MAVLink 2 compatibility flags.</param>
/// <param name="Payload">An exact copy of the wire payload.</param>
/// <param name="Signature">The original MAVLink 2 signature bytes, or an empty array.</param>
/// <param name="RawFrame">An exact copy of the complete wire frame.</param>
/// <param name="Definition">The selected-dialect definition when known.</param>
/// <param name="ReceivedAt">The receive timestamp.</param>
public sealed record RawMavLinkMessage(
    byte MavLinkVersion,
    byte SystemId,
    byte ComponentId,
    TransportEndPoint EndPoint,
    uint RawMessageId,
    byte Sequence,
    byte IncompatibilityFlags,
    byte CompatibilityFlags,
    byte[] Payload,
    byte[] Signature,
    byte[] RawFrame,
    MavLinkMessageDefinition? Definition,
    DateTimeOffset ReceivedAt)
    : MavLinkMessage(SystemId, ComponentId, RawMessageId, EndPoint, ReceivedAt)
{
    /// <summary>Gets the known dialect message name, if available.</summary>
    public string? MessageName => Definition?.Name;

    /// <summary>Gets whether the original frame carries a MAVLink 2 signature.</summary>
    public bool IsSigned => Signature.Length > 0;
}
