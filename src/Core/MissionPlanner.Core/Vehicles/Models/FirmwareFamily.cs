namespace MissionPlanner.Core.Vehicles.Models;

/// <summary>
/// Identifies the broad firmware family running on a vehicle.
/// </summary>
public enum FirmwareFamily
{
    /// <summary>The firmware family is not known.</summary>
    Unknown,
    /// <summary>ArduPilot Copter firmware.</summary>
    ArduCopter,
    /// <summary>ArduPilot Plane firmware.</summary>
    ArduPlane,
    /// <summary>ArduPilot Rover firmware.</summary>
    Rover,
    /// <summary>ArduPilot Sub firmware.</summary>
    ArduSub,
    /// <summary>ArduPilot antenna-tracker firmware.</summary>
    AntennaTracker,
    /// <summary>ArduPilot peripheral firmware.</summary>
    APPeriph,
    /// <summary>ArduPilot Blimp firmware.</summary>
    Blimp
}
