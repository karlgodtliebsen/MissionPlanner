namespace MissionPlanner.Simulation;

/// <summary>Signals an actionable simulator-to-vehicle connection failure.</summary>
public sealed class SimulationConnectionException : Exception
{
    /// <summary>Initializes a simulation connection failure.</summary>
    /// <param name="message">Actionable failure detail.</param>
    public SimulationConnectionException(string message)
        : base(message)
    {
    }
}
