namespace MissionPlanner.Core.Vehicles.Models;

/// <summary>
/// Identifies a safety-relevant action that can be requested for an active vehicle.
/// </summary>
public enum VehicleAction
{
    /// <summary>Arm the vehicle.</summary>
    Arm,
    /// <summary>Disarm the vehicle.</summary>
    Disarm,
    /// <summary>Change the flight mode.</summary>
    SetMode,
    /// <summary>Start an automatic takeoff.</summary>
    Takeoff,
    /// <summary>Land the vehicle.</summary>
    Land,
    /// <summary>Return the vehicle to its configured recovery location.</summary>
    ReturnToLaunch,
    /// <summary>Hold or loiter at the current location.</summary>
    Hold,
    /// <summary>Reboot the autopilot.</summary>
    RebootAutopilot,
    /// <summary>Set home to the current vehicle position.</summary>
    SetHomeHere,
    /// <summary>Execute a validated expert MAVLink command.</summary>
    ExpertCommand
}
