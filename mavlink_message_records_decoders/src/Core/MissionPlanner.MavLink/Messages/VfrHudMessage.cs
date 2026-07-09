using MissionPlanner.Transport;

namespace MissionPlanner.MavLink.Messages;

/// <summary>
/// Represents a MAVLink VFR_HUD message.
/// </summary>
public sealed record VfrHudMessage(
    byte SystemId,
    byte ComponentId,
    TransportEndPoint EndPoint,
    float Airspeed,
    float Groundspeed,
    short Heading,
    ushort Throttle,
    float Altitude,
    float Climb,
    DateTimeOffset ReceivedAt)
    : MavLinkMessage(SystemId, ComponentId, MessageIds.VfrHud, EndPoint, ReceivedAt);
