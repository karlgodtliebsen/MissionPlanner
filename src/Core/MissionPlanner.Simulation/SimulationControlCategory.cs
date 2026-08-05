namespace MissionPlanner.Simulation;

/// <summary>Identifies a simulation control category.</summary>
public enum SimulationControlCategory
{
    /// <summary>Weather or physical environment value.</summary>
    Environment,

    /// <summary>Simulated sensor state or reading.</summary>
    Sensor,

    /// <summary>Hazardous bounded failure injection.</summary>
    Fault
}
