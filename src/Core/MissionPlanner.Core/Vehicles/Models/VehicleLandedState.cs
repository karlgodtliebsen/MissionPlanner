namespace MissionPlanner.Core.Vehicles.Models;

/// <summary>Describes whether the vehicle is on the ground or in flight.</summary>
public enum VehicleLandedState : byte
{
    /// <summary>The flight controller did not provide a landed state.</summary>
    Undefined = 0,
    /// <summary>The vehicle is on the ground.</summary>
    OnGround = 1,
    /// <summary>The vehicle is in the air.</summary>
    InAir = 2,
    /// <summary>The vehicle is taking off.</summary>
    TakingOff = 3,
    /// <summary>The vehicle is landing.</summary>
    Landing = 4
}
