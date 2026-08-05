namespace MissionPlanner.Simulation.Abstractions;

/// <summary>Loads and saves scenario presets separately from simulator launch profiles.</summary>
public interface ISimulationScenarioPresetService
{
    /// <summary>Gets initialized presets.</summary>
    IReadOnlyList<SimulationScenarioPreset> Presets { get; }

    /// <summary>Loads persisted presets.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Loaded presets.</returns>
    ValueTask<IReadOnlyList<SimulationScenarioPreset>> InitializeAsync(
        CancellationToken cancellationToken = default);

    /// <summary>Adds or replaces one preset.</summary>
    /// <param name="preset">Preset to persist.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    ValueTask SaveAsync(
        SimulationScenarioPreset preset,
        CancellationToken cancellationToken = default);

    /// <summary>Deletes one preset.</summary>
    /// <param name="presetId">Preset identity.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    ValueTask DeleteAsync(Guid presetId, CancellationToken cancellationToken = default);
}
