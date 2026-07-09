using MissionPlanner.Transport;

namespace MissionPlanner.MavLink.Messages;

/// <summary>
/// Represents a MAVLink NavControllerOutput message.
/// </summary>
/// <param name="SystemId">The ID of the system that sent the message.</param>
/// <param name="ComponentId">The ID of the component that sent the message.</param>
/// <param name="EndPoint">The endpoint from which the message was received.</param>
/// <param name="NavRoll">The roll angle of the navigation controller.</param>
/// <param name="NavPitch">The pitch angle of the navigation controller.</param>
/// <param name="NavBearing">The bearing of the navigation controller.</param>
/// <param name="TargetBearing">The target bearing of the navigation controller.</param>
/// <param name="DistanceToWaypoint">The distance to the next waypoint.</param>
/// <param name="AltitudeError">The altitude error.</param>
/// <param name="AirspeedError">The airspeed error.</param>
/// <param name="CrosstrackError">The crosstrack error.</param>
/// <param name="ReceivedAt">The timestamp when the message was received.</param>
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
