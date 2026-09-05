using System.Collections.Concurrent;
using MissionPlanner.Core.ConfigTuning.Planner;

namespace MissionPlanner.Library.Browser;

/// <summary>
/// Keeps secrets in memory for this application instance. Reloading clears them;
/// browser storage does not provide the guarantees of a native credential vault.
/// </summary>
public sealed class BrowserPlannerSecretStore : IPlannerSecretStore
{
    private readonly ConcurrentDictionary<string, string> secrets = new(StringComparer.Ordinal);

    public ValueTask<string?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(secrets.TryGetValue(key, out var value) ? value : null);
    }

    public ValueTask SetAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);
        cancellationToken.ThrowIfCancellationRequested();
        secrets[key] = value;
        return ValueTask.CompletedTask;
    }

    public ValueTask RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        cancellationToken.ThrowIfCancellationRequested();
        secrets.TryRemove(key, out _);
        return ValueTask.CompletedTask;
    }
}
