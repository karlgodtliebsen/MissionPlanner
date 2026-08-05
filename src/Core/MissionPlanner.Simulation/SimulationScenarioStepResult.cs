namespace MissionPlanner.Simulation;

/// <summary>Identifies one step execution result.</summary>
public enum SimulationScenarioStepResult
{
    /// <summary>The step was validated but not executed during a dry run.</summary>
    Planned,

    /// <summary>The step completed successfully.</summary>
    Succeeded,

    /// <summary>The step failed or timed out.</summary>
    Failed,

    /// <summary>The step was canceled.</summary>
    Canceled
}
