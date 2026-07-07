using MissionPlanner.Core.Models;

namespace MissionPlanner.Core.Services.Abstractions;

/// <summary>
/// Service for sending MAVLink commands to vehicles.
/// </summary>
public interface IMavLinkCommandService
{
    /// <summary>
    /// Requests a telemetry data stream from the vehicle.
    /// </summary>
    /// <param name="vehicleId">Target vehicle identifier</param>
    /// <param name="streamId">MAVLink data stream ID (e.g., MAV_DATA_STREAM_EXTRA1 = 10 for ATTITUDE)</param>
    /// <param name="rateHz">Requested message rate in Hz</param>
    /// <param name="start">True to start streaming, false to stop</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if command was sent successfully</returns>
    Task<bool> RequestDataStreamAsync(
        VehicleId vehicleId,
        MavDataStream streamId,
        int rateHz,
        bool start = true,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// MAVLink data stream identifiers for REQUEST_DATA_STREAM command.
/// See: https://mavlink.io/en/messages/common.html#MAV_DATA_STREAM
/// </summary>
public enum MavDataStream : byte
{
    /// <summary>
    /// Enable all data streams
    /// </summary>
    All = 0,

    /// <summary>
    /// Enable IMU_RAW, GPS_RAW, GPS_STATUS packets
    /// </summary>
    RawSensors = 1,

    /// <summary>
    /// Enable GPS_STATUS, CONTROL_STATUS, AUX_STATUS
    /// </summary>
    ExtendedStatus = 2,

    /// <summary>
    /// Enable RC_CHANNELS_SCALED, RC_CHANNELS_RAW, SERVO_OUTPUT_RAW
    /// </summary>
    RcChannels = 3,

    /// <summary>
    /// Enable SERVO_OUTPUT_RAW
    /// </summary>
    RawController = 4,

    /// <summary>
    /// Enable LOCAL_POSITION, GLOBAL_POSITION_INT messages
    /// </summary>
    Position = 6,

    /// <summary>
    /// Enable ATTITUDE, ATTITUDE_QUATERNION messages (pitch, roll, yaw)
    /// </summary>
    Extra1 = 10,

    /// <summary>
    /// Enable VFR_HUD messages
    /// </summary>
    Extra2 = 11,

    /// <summary>
    /// Enable ATTITUDE_QUATERNION (for higher precision)
    /// </summary>
    Extra3 = 12,
}
