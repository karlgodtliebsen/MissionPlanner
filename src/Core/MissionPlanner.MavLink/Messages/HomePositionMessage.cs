using MissionPlanner.Transport;

namespace MissionPlanner.MavLink.Messages;

/// <summary>
/// Represents the vehicle's home (launch) position, sent when home is set or on request.
/// </summary>
/// <param name="SystemId">The ID of the system sending the message.</param>
/// <param name="ComponentId">The ID of the component sending the message.</param>
/// <param name="EndPoint">The endpoint from which the message was received.</param>
/// <param name="LatitudeDegrees">The home latitude in degrees.</param>
/// <param name="LongitudeDegrees">The home longitude in degrees.</param>
/// <param name="AltitudeMslMeters">The home altitude above mean sea level in meters.</param>
/// <param name="ReceivedAt">The timestamp when the message was received.</param>
public sealed record HomePositionMessage(
    byte SystemId,
    byte ComponentId,
    TransportEndPoint EndPoint,
    double LatitudeDegrees,
    double LongitudeDegrees,
    double AltitudeMslMeters,
    DateTimeOffset ReceivedAt)
    : MavLinkMessage(
        SystemId,
        ComponentId,
        MessageIds.HomePosition,
        EndPoint,
        ReceivedAt);
