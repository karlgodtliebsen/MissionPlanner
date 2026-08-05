namespace MissionPlanner.Simulation.Abstractions;

/// <summary>Represents one exact set of MissionPlanner-owned endpoint reservations.</summary>
public interface ISimulationPortLease : IAsyncDisposable
{
    /// <summary>Gets the reserved endpoints.</summary>
    IReadOnlyList<SimulationEndpoint> Endpoints { get; }
}
