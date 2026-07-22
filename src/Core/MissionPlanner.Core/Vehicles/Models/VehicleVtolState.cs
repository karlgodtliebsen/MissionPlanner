namespace MissionPlanner.Core.Vehicles.Models;

/// <summary>Describes the current VTOL mode or transition.</summary>
public enum VehicleVtolState : byte
{
    /// <summary>The state is unavailable or not applicable.</summary>
    Undefined = 0,
    /// <summary>The vehicle is transitioning to fixed-wing flight.</summary>
    TransitionToFixedWing = 1,
    /// <summary>The vehicle is transitioning to multicopter flight.</summary>
    TransitionToMulticopter = 2,
    /// <summary>The vehicle is in multicopter flight.</summary>
    Multicopter = 3,
    /// <summary>The vehicle is in fixed-wing flight.</summary>
    FixedWing = 4
}
