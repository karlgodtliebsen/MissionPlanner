namespace MissionPlanner.MavLink.Parameters;

/// <summary>
/// ArduPilot vehicle types for parameter metadata.
/// </summary>
public enum VehicleType
{
    /// <summary>
    /// ArduCopter (multirotor)
    /// </summary>
    ArduCopter,

    /// <summary>
    /// ArduPlane (fixed-wing)
    /// </summary>
    ArduPlane,

    /// <summary>
    /// Rover (ground vehicle)
    /// </summary>
    Rover,

    /// <summary>
    /// ArduSub (underwater vehicle)
    /// </summary>
    ArduSub,

    /// <summary>
    /// AntennaTracker
    /// </summary>
    AntennaTracker,

    /// <summary>
    /// AP_Periph (CAN peripheral)
    /// </summary>
    AP_Periph,

    /// <summary>
    /// SITL (Software In The Loop simulator)
    /// </summary>
    SITL,

    /// <summary>
    /// Blimp
    /// </summary>
    Blimp,

    /// <summary>
    /// Helicopter
    /// </summary>
    Heli
}
