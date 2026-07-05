namespace MissionPlanner.Core.Commands;

/// <summary>
/// Represents the result of a vehicle command. 
/// </summary>
public enum VehicleCommandResult
{
    Accepted = 0,
    TemporarilyRejected = 1,
    Denied = 2,
    Unsupported = 3,
    VehicleNotFound = 4,
    Failed = 5,
    Timeout = 100
}