using MissionPlanner.Shared.Models.Vehicles.Models;

namespace MissionPlanner.Simulation.Abstractions;

/// <summary>Executes one declarative scenario against an exact simulator target.</summary>
public interface ISimulationScenarioRunner
{
    /// <summary>Gets current observable runner state.</summary>
    SimulationScenarioRunnerSnapshot Current
    {
        get;
    }

    /// <summary>Occurs when runner state changes.</summary>
    event Action<SimulationScenarioRunnerChangedEventArgs>? Changed;

    /// <summary>Validates schema, exact target, modes, and controls without changing a vehicle.</summary>
    /// <param name="document">Scenario document.</param>
    /// <param name="sessionId">Exact simulation session.</param>
    /// <param name="vehicleId">Exact vehicle.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Dry-run validation evidence.</returns>
    Task<SimulationScenarioValidationReport> ValidateAsync(SimulationScenarioDocument document, Guid sessionId, VehicleId vehicleId, CancellationToken cancellationToken = default);

    /// <summary>Runs or dry-runs one scenario.</summary>
    /// <param name="request">Exact-target run request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Complete auditable report.</returns>
    Task<SimulationScenarioRunReport> RunAsync(SimulationScenarioRunRequest request, CancellationToken cancellationToken = default);

    /// <summary>Requests a pause at the next safe step boundary.</summary>
    /// <returns><see langword="true"/> when a running scenario accepted the request.</returns>
    bool Pause();

    /// <summary>Resumes a scenario paused between steps.</summary>
    /// <returns><see langword="true"/> when a paused scenario was resumed.</returns>
    bool Resume();
}
