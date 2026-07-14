using MissionPlanner.Transport;

namespace MissionPlanner.MavLink.Messages;

/// <summary>
/// Represents the global position of the drone, including latitude, longitude, and altitude.
/// </summary>
/// <param name="SystemId">The ID of the system sending the message.</param>
/// <param name="ComponentId">The ID of the component sending the message.</param>
/// <param name="LatitudeDegrees">The latitude in degrees.</param>
/// <param name="LongitudeDegrees">The longitude in degrees.</param>
/// <param name="AltitudeMslMeters">The altitude above mean sea level in meters.</param>
/// <param name="RelativeAltitudeMeters">The relative altitude in meters, if available.</param>
/// <param name="VelocityNorthMetersPerSecond">The northward velocity in meters per second, if available.</param>
/// <param name="VelocityEastMetersPerSecond">The eastward velocity in meters per second, if available.</param>
/// <param name="VelocityDownMetersPerSecond">The downward velocity in meters per second, if available.</param>
/// <param name="HeadingDegrees">The heading in degrees, if available.</param>
/// <param name="EndPoint">The endpoint from which the message was received.</param>
/// <param name="ReceivedAt">The timestamp when the message was received.</param>
/// <summary>
/// Represents the vehicle's filtered global position and velocity.
/// </summary>
public sealed record GlobalPositionIntMessage(
    byte SystemId,
    byte ComponentId,
    TransportEndPoint EndPoint,
    double LatitudeDegrees,
    double LongitudeDegrees,
    double AltitudeMslMeters,
    double? RelativeAltitudeMeters,
    double? VelocityNorthMetersPerSecond,
    double? VelocityEastMetersPerSecond,
    double? VelocityDownMetersPerSecond,
    double? HeadingDegrees,
    DateTimeOffset ReceivedAt)
    : MavLinkMessage(
        SystemId,
        ComponentId,
        MessageIds.GlobalPositionInt,
        EndPoint,
        ReceivedAt);
