namespace MissionPlanner.MavLink.Messages;

/// <summary>
/// Contains the IDs of MAVLink messages.
/// </summary>
public static class MessageIds
{
    /// <summary>
    /// 
    /// </summary>
    public const uint Heartbeat = 0;

    /// <summary>
    /// 
    /// </summary>
    public const uint SysStatus = 1;

    /// <summary>
    /// 
    /// </summary>
    public const uint Attitude = 30;

    /// <summary>
    ///
    /// </summary>
    public const uint GlobalPositionInt = 33;

    /// <summary>
    /// Request to read the onboard parameter with the param_id string id.
    /// </summary>
    public const uint ParamRequestRead = 20;

    /// <summary>
    /// Request all parameters of this component.
    /// </summary>
    public const uint ParamRequestList = 21;

    /// <summary>
    /// Emit the value of a onboard parameter.
    /// </summary>
    public const uint ParamValue = 22;

    /// <summary>
    /// Set a parameter value.
    /// </summary>
    public const uint ParamSet = 23;

    /// <summary>
    ///
    /// </summary>
    public const uint CommandLong = 76;

    /// <summary>
    /// 
    /// </summary>
    public const uint CommandAck = 77;


    public const uint GpsRawInt = 24;
    public const uint RawImu = 27;
    public const uint ScaledPressure = 29;
    public const uint LocalPositionNed = 32; //
    public const uint ServoOutputRaw = 36;
    public const uint MissionCurrent = 42;
    public const uint NavControllerOutput = 62;
    public const uint RcChannels = 65; //
    public const uint VfrHud = 74; //


    public const uint TimeSync = 111;

    public const uint PowerStatus = 125;
    public const uint BatteryStatus = 147; //
    public const uint MemInfo = 152;
    public const uint Ahrs2 = 178;
    public const uint EkfStatusReport = 193;

    /// <summary>
    /// 
    /// </summary>
    public const uint StatusText = 253;


    public const uint DefaultFallback = 65555;
}
