using System.Collections.Concurrent;
using MissionPlanner.Core.ConfigTuning.Planner;

namespace MissionPlanner.Library.Desktop;

/// <summary>
/// Keeps planner secrets in memory for the lifetime of the process. It is the fallback on platforms where
/// the operating system's secret storage is not available, so secrets are never written to disk.
/// </summary>
public sealed class SessionPlannerSecretStore : IPlannerSecretStore
{
    private readonly ConcurrentDictionary<string, string> secrets = new(StringComparer.Ordinal);

    /// <summary>Gets the secret stored under the key, or null when there is none.</summary>
    public ValueTask<string?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(secrets.TryGetValue(key, out var value) ? value : null);
    }

    /// <summary>Stores a secret under the key for the rest of the session.</summary>
    public ValueTask SetAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);
        cancellationToken.ThrowIfCancellationRequested();
        secrets[key] = value;
        return ValueTask.CompletedTask;
    }

    /// <summary>Removes the secret stored under the key.</summary>
    public ValueTask RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        cancellationToken.ThrowIfCancellationRequested();
        secrets.TryRemove(key, out _);
        return ValueTask.CompletedTask;
    }
}
