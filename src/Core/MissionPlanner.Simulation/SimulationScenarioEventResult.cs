namespace MissionPlanner.Simulation;

/// <summary>Identifies a simulation scenario event result.</summary>
public enum SimulationScenarioEventResult
{
    /// <summary>A requested value was confirmed.</summary>
    Applied,

    /// <summary>A control was explicitly reset.</summary>
    Reset,

    /// <summary>A hazardous control reached its duration and reset automatically.</summary>
    AutoReset,

    /// <summary>An operation failed or could not confirm readback.</summary>
    Failed
}
