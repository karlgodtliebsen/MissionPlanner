namespace MissionPlanner.Simulation;

/// <summary>Signals a deterministic resource-allocation conflict.</summary>
public sealed class SimulationAllocationException : Exception
{
    /// <summary>Initializes an allocation conflict.</summary>
    /// <param name="message">Actionable conflict detail.</param>
    public SimulationAllocationException(string message)
        : base(message)
    {
    }
}
