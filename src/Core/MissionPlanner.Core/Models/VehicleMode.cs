namespace MissionPlanner.Core.Models;

/// <summary>
/// Represents the mode of a vehicle.
/// </summary>
public enum VehicleMode
{
    Unknown = 0,
    Stabilize,
    AltHold,
    Loiter,
    Guided,
    Rtl,
    Land
}