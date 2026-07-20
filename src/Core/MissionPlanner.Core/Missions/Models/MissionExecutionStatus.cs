namespace MissionPlanner.Core.Missions.Models;

/// <summary>
/// Provides the public API for MissionExecutionStatus.
/// </summary>
/// <summary>
/// Provides the public API for Completed.
/// </summary>
public enum MissionExecutionStatus
{
    /// <summary>The execution state is unknown.</summary>
    Unknown,
    /// <summary>The mission is loaded.</summary>
    Loaded,
    /// <summary>The mission is ready to run.</summary>
    Ready,
    /// <summary>The mission is running.</summary>
    Running,
    /// <summary>The mission is paused.</summary>
    Paused,
    /// <summary>The mission completed successfully.</summary>
    Completed,
    /// <summary>The mission failed.</summary>
    Failed
}
