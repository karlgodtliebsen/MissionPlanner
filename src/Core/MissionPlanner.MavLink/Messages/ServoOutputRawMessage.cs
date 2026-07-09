using MissionPlanner.Transport;

namespace MissionPlanner.MavLink.Messages;

/// <summary>
/// Represents a MAVLink SERVO_OUTPUT_RAW message.
/// </summary>
public sealed record ServoOutputRawMessage(
    byte SystemId,
    byte ComponentId,
    TransportEndPoint EndPoint,
    uint TimeUsec,
    byte Port,
    IReadOnlyList<ushort> ServoRaw,
    DateTimeOffset ReceivedAt)
    : MavLinkMessage(SystemId, ComponentId, MessageIds.ServoOutputRaw, EndPoint, ReceivedAt);
