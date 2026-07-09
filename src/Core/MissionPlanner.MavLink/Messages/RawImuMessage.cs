using MissionPlanner.Transport;

namespace MissionPlanner.MavLink.Messages;

/// <summary>
/// Represents a MAVLink RAW_IMU message.
/// </summary>
public sealed record RawImuMessage(
    byte SystemId,
    byte ComponentId,
    TransportEndPoint EndPoint,
    ulong TimeUsec,
    short XAcc,
    short YAcc,
    short ZAcc,
    short XGyro,
    short YGyro,
    short ZGyro,
    short XMag,
    short YMag,
    short ZMag,
    byte? Id,
    short? Temperature,
    DateTimeOffset ReceivedAt)
    : MavLinkMessage(SystemId, ComponentId, MessageIds.RawImu, EndPoint, ReceivedAt);
