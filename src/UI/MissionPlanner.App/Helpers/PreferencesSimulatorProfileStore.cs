using MissionPlanner.Core.Simulation;

namespace MissionPlanner.App.Helpers;

/// <summary>Persists simulator profiles through platform Preferences.</summary>
public sealed class PreferencesSimulatorProfileStore : ISimulatorProfileStore
{
    private const string PreferenceKey = "simulation.profiles.v1";

    /// <inheritdoc />
    public ValueTask<string?> ReadAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var value = Preferences.Default.Get(PreferenceKey, string.Empty);
        return ValueTask.FromResult<string?>(string.IsNullOrWhiteSpace(value) ? null : value);
    }

    /// <inheritdoc />
    public ValueTask WriteAsync(string document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        cancellationToken.ThrowIfCancellationRequested();
        Preferences.Default.Set(PreferenceKey, document);
        return ValueTask.CompletedTask;
    }
}
