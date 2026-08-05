namespace MissionPlanner.Core.Simulation;

/// <summary>Identifies a named state used by a wait-for-state step.</summary>
public enum SimulationVehicleStateRequirement
{
    /// <summary>The exact vehicle is online.</summary>
    Online,

    /// <summary>The exact vehicle is armed.</summary>
    Armed,

    /// <summary>The exact vehicle is disarmed.</summary>
    Disarmed,

    /// <summary>The vehicle reports that it is on the ground.</summary>
    OnGround,

    /// <summary>The vehicle reports that it is in the air.</summary>
    InAir
}
