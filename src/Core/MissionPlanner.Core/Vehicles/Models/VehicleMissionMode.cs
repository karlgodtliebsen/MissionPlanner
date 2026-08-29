namespace MissionPlanner.Core.Vehicles.Models;

/// <summary>Describes whether mission execution is enabled by the current vehicle mode.</summary>
public enum VehicleMissionMode : byte
{
    Unknown = 0,
    Mission = 1,
    Suspended = 2
}
