namespace MissionPlanner.Firmware.Model;

/// <summary>Identifies the vehicle family supported by firmware.</summary>
public enum FirmwareVehicleType
{
    /// <summary>A multicopter.</summary>
    Copter,

    /// <summary>A helicopter.</summary>
    Helicopter,

    /// <summary>A fixed-wing aircraft.</summary>
    Plane,

    /// <summary>A ground or surface vehicle.</summary>
    Rover,

    /// <summary>An underwater vehicle.</summary>
    Sub,

    /// <summary>An antenna tracker.</summary>
    AntennaTracker,

    /// <summary>An airship.</summary>
    Blimp,

    /// <summary>An unrecognized vehicle family.</summary>
    Unknown
}
