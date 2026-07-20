namespace MissionPlanner.MavLink.Messages;

/// <summary>
/// Contains the IDs of MAVLink messages.
/// Contains the numeric identifiers of MAVLink messages used by Mission Planner.
/// </summary>
/// <remarks>
/// Message IDs are defined by the MAVLink Common and ArduPilotMega dialects.
/// </remarks>
public static class MessageIds
{
    /// <summary>
    /// Periodic vehicle heartbeat containing type, autopilot, mode,
    /// arming state and system status.
    /// </summary>
    public const uint Heartbeat = 0;

    /// <summary>
    /// General system status, including sensor and battery information.
    /// </summary>
    public const uint SysStatus = 1;


    /// <summary>
    /// Requests one parameter by name or index.
    /// </summary>
    public const uint ParamRequestRead = 20;

    /// <summary>
    /// Requests all parameters from a component.
    /// </summary>
    public const uint ParamRequestList = 21;

    /// <summary>
    /// Reports the value and metadata of one parameter.
    /// </summary>
    public const uint ParamValue = 22;

    /// <summary>
    /// Sets the value of one parameter.
    /// </summary>
    public const uint ParamSet = 23;


    /// <summary>
    /// Raw GPS position, fix status, velocity and satellite information.
    /// </summary>
    public const uint GpsRawInt = 24;

    /// <summary>
    /// Raw inertial measurement unit data.
    /// </summary>
    public const uint RawImu = 27;

    /// <summary>
    /// Scaled atmospheric pressure and temperature data.
    /// </summary>
    public const uint ScaledPressure = 29;

    /// <summary>
    /// Vehicle attitude and angular rates.
    /// </summary>
    public const uint Attitude = 30;

    /// <summary>
    /// Vehicle position and velocity in the local NED coordinate frame.
    /// </summary>
    public const uint LocalPositionNed = 32;

    /// <summary>
    /// Filtered global position, relative altitude, velocity and heading.
    /// </summary>
    public const uint GlobalPositionInt = 33;

    /// <summary>
    /// Raw servo or motor output values.
    /// </summary>
    public const uint ServoOutputRaw = 36;


    /// <summary>
    /// Requests a subset of mission items using the legacy floating-point format.
    /// </summary>
    public const uint MissionRequestPartialList = 37;

    /// <summary>
    /// Requests replacement of a subset of mission items.
    /// </summary>
    public const uint MissionWritePartialList = 38;

    /// <summary>
    /// Legacy floating-point mission item.
    /// Prefer <see cref="MissionItemInt"/>.
    /// </summary>
    public const uint MissionItem = 39;

    /// <summary>
    /// Legacy request for one mission item.
    /// Prefer <see cref="MissionRequestInt"/>.
    /// </summary>
    public const uint MissionRequest = 40;

    /// <summary>
    /// Requests that the vehicle changes the current mission sequence.
    /// </summary>
    public const uint MissionSetCurrent = 41;

    /// <summary>
    /// Reports the currently active mission item.
    /// </summary>
    public const uint MissionCurrent = 42;

    /// <summary>
    /// Requests the complete mission item list.
    /// </summary>
    public const uint MissionRequestList = 43;

    /// <summary>
    /// Reports or announces the number of mission items.
    /// </summary>
    public const uint MissionCount = 44;

    /// <summary>
    /// Clears mission items stored by the vehicle.
    /// </summary>
    public const uint MissionClearAll = 45;

    /// <summary>
    /// Reports that a mission item has been reached.
    /// </summary>
    public const uint MissionItemReached = 46;

    /// <summary>
    /// Acknowledges a mission protocol operation.
    /// </summary>
    public const uint MissionAck = 47;

    /// <summary>
    /// Requests one mission item using integer coordinates.
    /// </summary>
    public const uint MissionRequestInt = 51;

    /// <summary>
    /// Reports that stored mission content has changed.
    /// </summary>
    public const uint MissionChanged = 52;

    /// <summary>
    /// Mission item using integer latitude and longitude coordinates.
    /// </summary>
    public const uint MissionItemInt = 73;


    /// <summary>
    /// Navigation-controller output, including bearings, waypoint distance
    /// and navigation errors.
    /// </summary>
    public const uint NavControllerOutput = 62;

    /// <summary>
    /// Received RC channel values and signal strength.
    /// </summary>
    public const uint RcChannels = 65;

    /// <summary>
    /// Compact HUD-oriented telemetry containing speed, heading,
    /// altitude and climb rate.
    /// </summary>
    public const uint VfrHud = 74;

    /// <summary>
    /// The vehicle's home (launch) position.
    /// </summary>
    public const uint HomePosition = 242;


    /// <summary>
    /// Sends a MAVLink command with seven command parameters.
    /// </summary>
    public const uint CommandLong = 76;

    /// <summary>
    /// Reports the result of a previously issued command.
    /// </summary>
    public const uint CommandAck = 77;


    /// <summary>
    /// File transfer protocol message.
    /// </summary>
    public const uint FileTransferProtocol = 110;


    /// <summary>
    /// Synchronizes timestamps between MAVLink systems.
    /// </summary>
    public const uint TimeSync = 111;

    /// <summary>
    /// Reports controller and servo rail power status.
    /// </summary>
    public const uint PowerStatus = 125;

    /// <summary>
    /// Detailed battery voltage, current, consumption and remaining capacity.
    /// </summary>
    public const uint BatteryStatus = 147;


    /// <summary>
    /// ArduPilot memory usage information.
    /// </summary>
    public const uint MemInfo = 152;

    /// <summary>
    /// Secondary ArduPilot AHRS estimate containing attitude,
    /// altitude and geographic position.
    /// </summary>
    public const uint Ahrs2 = 178;

    /// <summary>
    /// ArduPilot EKF health and variance report.
    /// </summary>
    public const uint EkfStatusReport = 193;


    /// <summary>
    /// Human-readable status, warning or error text.
    /// </summary>
    public const uint StatusText = 253;


    /// <summary>
    /// Provides the public API for DefaultFallback.
    /// </summary>
    public const uint DefaultFallback = 65555;
}
