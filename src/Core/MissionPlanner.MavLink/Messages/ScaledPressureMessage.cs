using MissionPlanner.Transport;

namespace MissionPlanner.MavLink.Messages;

/// <summary>
/// Represents a MAVLink ScaledPressure message.
/// </summary>
/// <param name="SystemId">The ID of the system that sent the message.</param>
/// <param name="ComponentId">The ID of the component that sent the message.</param>
/// <param name="EndPoint">The endpoint from which the message was received.</param>
/// <param name="TimeBootMs">The time since system boot in milliseconds.</param>
/// <param name="PressureAbsolute">The absolute pressure.</param>
/// <param name="PressureDifferential">The differential pressure.</param>
/// <param name="Temperature">The temperature.</param>
/// <param name="DifferentialTemperature">The differential temperature.</param>
/// <param name="ReceivedAt">The timestamp when the message was received.</param>
public sealed record ScaledPressureMessage(
    byte SystemId,
    byte ComponentId,
    TransportEndPoint EndPoint,
    uint TimeBootMs,
    float PressureAbsolute,
    float PressureDifferential,
    short Temperature,
    short DifferentialTemperature,
    DateTimeOffset ReceivedAt)
    : MavLinkMessage(SystemId, ComponentId, MessageIds.ScaledPressure, EndPoint, ReceivedAt);
