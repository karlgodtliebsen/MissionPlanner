namespace MissionPlanner.Simulation.Abstractions;

/// <summary>Persists an opaque simulation scenario-preset document.</summary>
public interface ISimulationScenarioPresetStore
{
    /// <summary>Reads the persisted preset document.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The document, or <see langword="null"/>.</returns>
    ValueTask<string?> ReadAsync(CancellationToken cancellationToken = default);

    /// <summary>Atomically replaces the persisted preset document.</summary>
    /// <param name="document">Serialized preset document.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    ValueTask WriteAsync(string document, CancellationToken cancellationToken = default);
}
