using MissionPlanner.Core.ConfigTuning.Planner;
using MissionPlanner.Maps.Credentials;

namespace MissionPlanner.App.Services;

/// <summary>Adapts the existing Planner secure secret store for map credentials.</summary>
public sealed class PlannerMapSecretStoreAdapter(IPlannerSecretStore secretStore) : IMapSecretStore
{
    /// <inheritdoc />
    public ValueTask<string?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        return secretStore.GetAsync(key, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask SetAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        return secretStore.SetAsync(key, value, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        return secretStore.RemoveAsync(key, cancellationToken);
    }
}
