using MissionPlanner.Transport;

namespace MissionPlanner.MavLink.Messages;

/// <summary>
/// Represents a MAVLink NAV_CONTROLLER_OUTPUT message.
/// </summary>
public sealed record NavControllerOutputMessage(
    byte SystemId,
    byte ComponentId,
    TransportEndPoint EndPoint,
    float NavRoll,
    float NavPitch,
    short NavBearing,
    short TargetBearing,
    ushort DistanceToWaypoint,
    float AltitudeError,
    float AirspeedError,
    float CrosstrackError,
    DateTimeOffset ReceivedAt)
    : MavLinkMessage(SystemId, ComponentId, MessageIds.NavControllerOutput, EndPoint, ReceivedAt);
