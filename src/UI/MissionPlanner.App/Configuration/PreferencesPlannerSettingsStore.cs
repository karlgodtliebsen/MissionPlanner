using MissionPlanner.Core.ConfigTuning.Planner;

namespace MissionPlanner.App.Configuration;

/// <summary>Persists the non-secret Planner settings document through MAUI Preferences.</summary>
public sealed class PreferencesPlannerSettingsStore : IPlannerSettingsStore
{
    private const string PreferenceKey = "planner.settings.v2";

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

    /// <inheritdoc />
    public ValueTask ClearAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Preferences.Default.Remove(PreferenceKey);
        return ValueTask.CompletedTask;
    }
}
