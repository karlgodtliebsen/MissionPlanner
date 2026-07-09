using MissionPlanner.Transport;

namespace MissionPlanner.MavLink.Messages;

/// <summary>
/// Represents a MAVLink GPS_RAW_INT message.
/// </summary>
public sealed record GpsRawIntMessage(
    byte SystemId,
    byte ComponentId,
    TransportEndPoint EndPoint,
    ulong TimeUsec,
    byte FixType,
    double Latitude,
    double Longitude,
    double Altitude,
    ushort Eph,
    ushort Epv,
    ushort Velocity,
    ushort CourseOverGround,
    byte SatellitesVisible,
    double? AltitudeEllipsoid,
    uint? HorizontalAccuracy,
    uint? VerticalAccuracy,
    uint? VelocityAccuracy,
    uint? HeadingAccuracy,
    double? Yaw,
    DateTimeOffset ReceivedAt)
    : MavLinkMessage(SystemId, ComponentId, MessageIds.GpsRawInt, EndPoint, ReceivedAt);
