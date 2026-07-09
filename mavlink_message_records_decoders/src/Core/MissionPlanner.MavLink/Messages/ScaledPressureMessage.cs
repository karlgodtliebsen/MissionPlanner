using MissionPlanner.Transport;

namespace MissionPlanner.MavLink.Messages;

/// <summary>
/// Represents a MAVLink SCALED_PRESSURE message.
/// </summary>
public sealed record ScaledPressureMessage(
    byte SystemId,
    byte ComponentId,
    TransportEndPoint EndPoint,
    uint TimeBootMs,
    float PressureAbsolute,
    float PressureDifferential,
    short Temperature,
    short? DifferentialTemperature,
    DateTimeOffset ReceivedAt)
    : MavLinkMessage(SystemId, ComponentId, MessageIds.ScaledPressure, EndPoint, ReceivedAt);
