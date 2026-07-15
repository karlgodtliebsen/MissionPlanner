namespace MissionPlanner.Core.Models;

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
    Extra3 = 12
}
