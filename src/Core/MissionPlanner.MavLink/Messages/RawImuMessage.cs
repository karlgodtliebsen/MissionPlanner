using MissionPlanner.Transport;

namespace MissionPlanner.MavLink.Messages;

/// <summary>
/// Represents a MAVLink RawImu message.
/// </summary>
/// <param name="SystemId">The ID of the system that sent the message.</param>
/// <param name="ComponentId">The ID of the component that sent the message.</param>
/// <param name="EndPoint">The endpoint from which the message was received.</param>
/// <param name="TimeUsec">The timestamp in microseconds.</param>
/// <param name="XAcc">The X-axis acceleration.</param>
/// <param name="YAcc">The Y-axis acceleration.</param>
/// <param name="ZAcc">The Z-axis acceleration.</param>
/// <param name="XGyro">The X-axis gyroscope reading.</param>
/// <param name="YGyro">The Y-axis gyroscope reading.</param>
/// <param name="ZGyro">The Z-axis gyroscope reading.</param>
/// <param name="XMag">The X-axis magnetometer reading.</param>
/// <param name="YMag">The Y-axis magnetometer reading.</param>
/// <param name="ZMag">The Z-axis magnetometer reading.</param>
/// <param name="Id">The ID of the IMU.</param>
/// <param name="Temperature">The temperature of the IMU.</param>
/// <param name="ReceivedAt">The timestamp when the message was received.</param>
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
    byte Id,
    short Temperature,
    DateTimeOffset ReceivedAt)
    : MavLinkMessage(SystemId, ComponentId, MessageIds.RawImu, EndPoint, ReceivedAt);
