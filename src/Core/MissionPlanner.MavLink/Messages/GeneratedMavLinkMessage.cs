using MissionPlanner.Transport;

namespace MissionPlanner.MavLink.Messages;

/// <summary>
/// Base type for generated, dialect-backed MAVLink wire messages.
/// </summary>
/// <param name="SystemId">The source MAVLink system ID.</param>
/// <param name="ComponentId">The source MAVLink component ID.</param>
/// <param name="MessageId">The numeric MAVLink message ID.</param>
/// <param name="EndPoint">The source transport endpoint.</param>
/// <param name="ReceivedAt">The reception timestamp.</param>
public abstract record GeneratedMavLinkMessage(
    byte SystemId,
    byte ComponentId,
    uint MessageId,
    TransportEndPoint EndPoint,
    DateTimeOffset ReceivedAt)
    : MavLinkMessage(SystemId, ComponentId, MessageId, EndPoint, ReceivedAt)
{
    /// <summary>
    /// Serializes the message fields in MAVLink wire order.
    /// </summary>
    /// <param name="truncateExtensions">
    /// Whether trailing zero bytes may be omitted down to the message's minimum payload length.
    /// </param>
    /// <returns>The serialized MAVLink payload.</returns>
    public byte[] EncodePayload(bool truncateExtensions = true)
    {
        var payload = new byte[MaximumPayloadLength];
        WritePayload(payload);
        if (!truncateExtensions || MinimumPayloadLength == MaximumPayloadLength)
        {
            return payload;
        }

        var length = payload.Length;
        while (length > MinimumPayloadLength && payload[length - 1] == 0)
        {
            length--;
        }

        return length == payload.Length ? payload : payload[..length];
    }

    internal abstract int MinimumPayloadLength { get; }

    internal abstract int MaximumPayloadLength { get; }

    internal abstract void WritePayload(Span<byte> payload);
}
