namespace MissionPlanner.MavLink;

/// <summary>
/// Defines the severity levels for MAVLink statustext messages.
/// </summary>
public enum MavSeverity : byte
{
    Emergency = 0,
    Alert = 1,
    Critical = 2,
    Error = 3,
    Warning = 4,
    Notice = 5,
    Info = 6,
    Debug = 7
}