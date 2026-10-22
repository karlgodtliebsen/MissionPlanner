namespace MissionPlanner.Core.Vehicles.Models;

/// <summary>Describes whether mission execution is enabled by the current vehicle mode.</summary>
public enum VehicleMissionMode : byte
{
    /// <summary>The mission state is not known.</summary>
    Unknown = 0,
    /// <summary>The vehicle is executing a mission.</summary>
    Mission = 1,
    /// <summary>The mission is suspended.</summary>
    Suspended = 2
}
