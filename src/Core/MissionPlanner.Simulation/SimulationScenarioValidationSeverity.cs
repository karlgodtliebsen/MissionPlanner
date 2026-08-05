namespace MissionPlanner.Simulation;

/// <summary>Identifies scenario validation severity.</summary>
public enum SimulationScenarioValidationSeverity
{
    /// <summary>Execution cannot proceed.</summary>
    Error,

    /// <summary>Execution can proceed with an explicit limitation.</summary>
    Warning
}
