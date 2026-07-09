using MissionPlanner.Transport;

namespace MissionPlanner.MavLink.Messages;

/// <summary>
/// Represents a MAVLink GpsRawInt message.
/// </summary>
/// <param name="SystemId">The ID of the system that sent the message.</param>
/// <param name="ComponentId">The ID of the component that sent the message.</param>
/// <param name="EndPoint">The endpoint from which the message was received.</param>
/// <param name="TimeUsec">The timestamp in microseconds.</param>
/// <param name="FixType">The type of GPS fix.</param>
/// <param name="Latitude">The latitude in degrees * 1E7.</param>
/// <param name="Longitude">The longitude in degrees * 1E7.</param>
/// <param name="Altitude">The altitude in millimeters.</param>
/// <param name="Eph">The GPS HDOP horizontal dilution of position in cm (m*100).</param>
/// <param name="Epv">The GPS VDOP vertical dilution of position in cm (m*100).</param>
/// <param name="Velocity">The GPS ground speed (m/s * 100).</param>
/// <param name="CourseOverGround">The course over ground (NOT heading, but direction of movement) in degrees * 100.</param>
/// <param name="SatellitesVisible">The number of satellites visible.</param>
/// <param name="ReceivedAt">The timestamp when the message was received.</param>
public sealed record GpsRawIntMessage(
    byte SystemId,
    byte ComponentId,
    TransportEndPoint EndPoint,
    ulong TimeUsec,
    byte FixType,
    int Latitude,
    int Longitude,
    int Altitude,
    ushort Eph,
    ushort Epv,
    ushort Velocity,
    ushort CourseOverGround,
    byte SatellitesVisible,
    DateTimeOffset ReceivedAt)
    : MavLinkMessage(SystemId, ComponentId, MessageIds.GpsRawInt, EndPoint, ReceivedAt);
