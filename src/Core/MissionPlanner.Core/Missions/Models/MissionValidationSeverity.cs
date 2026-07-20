namespace MissionPlanner.Core.Missions.Models;

/// <summary>
/// Provides the public API for MissionValidationSeverity.
/// </summary>
/// <summary>
/// Provides the public API for Error.
/// </summary>
public enum MissionValidationSeverity
{
    /// <summary>An informational validation message.</summary>
    Information,
    /// <summary>A validation warning.</summary>
    Warning,
    /// <summary>A validation error.</summary>
    Error
}
