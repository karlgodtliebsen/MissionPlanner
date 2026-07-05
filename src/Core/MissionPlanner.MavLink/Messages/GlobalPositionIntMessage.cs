using System.Net;

namespace MissionPlanner.MavLink.Messages;

/// <summary>
/// Represents the global position of the drone, including latitude, longitude, and altitude.
/// </summary>
/// <param name="SystemId">The ID of the system sending the message.</param>
/// <param name="ComponentId">The ID of the component sending the message.</param>
/// <param name="Latitude">The latitude in degrees.</param>
/// <param name="Longitude">The longitude in degrees.</param>
/// <param name="Altitude">The altitude in meters.</param>
/// <param name="IPEndPoint">The IP endpoint from which the message was received.</param>
/// <param name="ReceivedAt">The timestamp when the message was received.</param>
public sealed record GlobalPositionIntMessage(
    byte SystemId,
    byte ComponentId,
    IPEndPoint IPEndPoint,
    double Latitude,
    double Longitude,
    double Altitude,
    DateTimeOffset ReceivedAt)
    : MavLinkMessage(
        SystemId,
        ComponentId,
        MessageIds.GlobalPositionInt,
        IPEndPoint,
        ReceivedAt);