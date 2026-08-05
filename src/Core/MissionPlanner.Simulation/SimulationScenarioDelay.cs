using MissionPlanner.Simulation.Abstractions;

namespace MissionPlanner.Simulation;

/// <summary>Provides real cancellable delays for scenario telemetry polling.</summary>
public sealed class SimulationScenarioDelay : ISimulationScenarioDelay
{
    /// <inheritdoc />
    public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        return Task.Delay(delay, cancellationToken);
    }
}
