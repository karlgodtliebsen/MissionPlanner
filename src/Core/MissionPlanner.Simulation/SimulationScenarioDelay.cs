namespace MissionPlanner.Core.Simulation;

/// <summary>Provides real cancellable delays for scenario telemetry polling.</summary>
public sealed class SimulationScenarioDelay : ISimulationScenarioDelay
{
    /// <inheritdoc />
    public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken) =>
        Task.Delay(delay, cancellationToken);
}
