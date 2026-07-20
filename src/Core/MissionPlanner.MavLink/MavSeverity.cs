namespace MissionPlanner.MavLink;

/// <summary>
/// Defines the severity levels for MAVLink statustext messages.
/// </summary>
public enum MavSeverity : byte
{
    /// <summary>
    /// Provides the public API for Emergency.
    /// </summary>
    Emergency = 0,
    /// <summary>
    /// Provides the public API for Alert.
    /// </summary>
    Alert = 1,
    /// <summary>
    /// Provides the public API for Critical.
    /// </summary>
    Critical = 2,
    /// <summary>
    /// Provides the public API for Error.
    /// </summary>
    Error = 3,
    /// <summary>
    /// Provides the public API for Warning.
    /// </summary>
    Warning = 4,
    /// <summary>
    /// Provides the public API for Notice.
    /// </summary>
    Notice = 5,
    /// <summary>
    /// Provides the public API for Info.
    /// </summary>
    Info = 6,
    /// <summary>
    /// Provides the public API for Debug.
    /// </summary>
    Debug = 7
}
