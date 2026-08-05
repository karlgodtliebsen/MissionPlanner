namespace MissionPlanner.Core.Simulation;

/// <summary>Identifies overall scenario execution result.</summary>
public enum SimulationScenarioRunResult
{
    /// <summary>All steps completed successfully.</summary>
    Succeeded,

    /// <summary>Dry-run validation completed without vehicle-changing actions.</summary>
    DryRun,

    /// <summary>Validation or execution failed.</summary>
    Failed,

    /// <summary>The caller canceled the run.</summary>
    Canceled
}
