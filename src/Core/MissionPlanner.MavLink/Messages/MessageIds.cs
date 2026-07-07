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

    /// <summary>
    /// 
    /// </summary>
    public const uint StatusText = 253;
}