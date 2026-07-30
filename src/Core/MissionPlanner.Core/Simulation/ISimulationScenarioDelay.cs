namespace MissionPlanner.Core.Simulation;

/// <summary>Provides cancellable delays for scenario wait polling.</summary>
public interface ISimulationScenarioDelay
{
    /// <summary>Waits for a bounded interval.</summary>
    /// <param name="delay">Delay duration.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken);
}
