namespace MissionPlanner.Core.Simulation;

/// <summary>Identifies the result of a safe orphan recovery attempt.</summary>
public enum SimulationOrphanRecoveryState
{
    /// <summary>The process no longer exists.</summary>
    NotRunning,

    /// <summary>The exact path and start time matched and the process was stopped.</summary>
    Recovered,

    /// <summary>The PID exists but its path or start time did not match, so it was not touched.</summary>
    IdentityMismatch,

    /// <summary>The exact process could not be inspected or stopped.</summary>
    Failed
}
