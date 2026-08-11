namespace MissionPlanner.Maps.Credentials;

/// <summary>Stores map secrets in a platform secure store.</summary>
public interface IMapSecretStore
{
    /// <summary>Reads a secret.</summary>
    ValueTask<string?> GetAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>Writes a secret.</summary>
    ValueTask SetAsync(string key, string value, CancellationToken cancellationToken = default);

    /// <summary>Removes a secret.</summary>
    ValueTask RemoveAsync(string key, CancellationToken cancellationToken = default);
}
