using MissionPlanner.Simulation.Abstractions;

namespace MissionPlanner.AvaloniaUI.App.Services;

/// <summary>Persists simulation scenario presets in platform application preferences.</summary>
public sealed class PreferencesSimulationScenarioPresetStore : ISimulationScenarioPresetStore
{
    private const string PreferencesKey = "simulation.scenario-presets.v1";

    /// <inheritdoc />
    public ValueTask<string?> ReadAsync(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();

        //cancellationToken.ThrowIfCancellationRequested();
        //return ValueTask.FromResult(Preferences.Default.Get<string?>(PreferencesKey, null));
    }

    /// <inheritdoc />
    public ValueTask WriteAsync(string document, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
        //ArgumentNullException.ThrowIfNull(document);
        //cancellationToken.ThrowIfCancellationRequested();
        //Preferences.Default.Set(PreferencesKey, document);
        //return ValueTask.CompletedTask;
    }
}
