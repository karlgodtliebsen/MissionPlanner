using MissionPlanner.Core.Simulation;

namespace MissionPlanner.App.Helpers;

/// <summary>Persists simulation scenario presets in platform application preferences.</summary>
public sealed class PreferencesSimulationScenarioPresetStore : ISimulationScenarioPresetStore
{
    private const string PreferencesKey = "simulation.scenario-presets.v1";

    /// <inheritdoc />
    public ValueTask<string?> ReadAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(Preferences.Default.Get<string?>(PreferencesKey, null));
    }

    /// <inheritdoc />
    public ValueTask WriteAsync(string document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        cancellationToken.ThrowIfCancellationRequested();
        Preferences.Default.Set(PreferencesKey, document);
        return ValueTask.CompletedTask;
    }
}
