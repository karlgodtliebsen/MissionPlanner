namespace MissionPlanner.Core.Simulation;

/// <summary>Identifies observable scenario-runner state.</summary>
public enum SimulationScenarioRunnerState
{
    /// <summary>No run is active.</summary>
    Idle,

    /// <summary>The scenario and live capabilities are being validated.</summary>
    Validating,

    /// <summary>A step is executing.</summary>
    Running,

    /// <summary>A pause will occur after the current step reaches a safe boundary.</summary>
    PauseRequested,

    /// <summary>Execution is paused between steps.</summary>
    Paused,

    /// <summary>The run completed successfully.</summary>
    Completed,

    /// <summary>The run failed.</summary>
    Failed,

    /// <summary>The run was canceled.</summary>
    Canceled
}
