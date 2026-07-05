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