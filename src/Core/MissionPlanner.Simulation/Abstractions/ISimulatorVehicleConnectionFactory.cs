namespace MissionPlanner.Simulation.Abstractions;

/// <summary>Creates an independently owned vehicle connection for one simulator session.</summary>
public interface ISimulatorVehicleConnectionFactory
{
    /// <summary>Creates a connection that cannot replace or disconnect another session's transport.</summary>
    /// <param name="sessionId">The owning simulation session identity.</param>
    /// <returns>The isolated simulator vehicle connection.</returns>
    ISimulatorVehicleConnection Create(Guid sessionId);
}
