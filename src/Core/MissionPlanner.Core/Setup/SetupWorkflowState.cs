namespace MissionPlanner.Core.Setup;

/// <summary>Describes the current state of a setup workflow.</summary>
public enum SetupWorkflowState
{
    /// <summary>The workflow can be opened now.</summary>
    Available,
    /// <summary>The connected vehicle does not support the workflow.</summary>
    Unsupported,
    /// <summary>No online vehicle is available.</summary>
    NotConnected,
    /// <summary>The workflow is supported but its recommended prerequisites are incomplete.</summary>
    NotStarted,
    /// <summary>The workflow is currently running.</summary>
    InProgress,
    /// <summary>Completion evidence remains valid for the current vehicle.</summary>
    Completed,
    /// <summary>Previous completion evidence needs review.</summary>
    Warning,
    /// <summary>The latest workflow operation failed.</summary>
    Failed
}
