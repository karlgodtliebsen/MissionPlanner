using MissionPlanner.Maps.Catalog;

namespace MissionPlanner.Maps.Credentials;

/// <summary>Manages source credentials without returning secrets to callers.</summary>
public sealed class MapCredentialService(IMapSecretStore secretStore, IMapCredentialTester credentialTester)
{
    /// <summary>Gets configuration state for a source.</summary>
    public async ValueTask<MapCredentialState> GetStateAsync(MapSourceDefinition source, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.CredentialRequirement == MapCredentialRequirement.None)
        {
            return new MapCredentialState(source.CredentialRequirement, true);
        }

        return new MapCredentialState(source.CredentialRequirement, !string.IsNullOrEmpty(await secretStore.GetAsync(GetKey(source.Id), cancellationToken).ConfigureAwait(false)));
    }

    /// <summary>Stores a source credential in secure storage.</summary>
    public ValueTask SetAsync(MapSourceDefinition source, string credential, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(credential);
        if (source.CredentialRequirement == MapCredentialRequirement.None)
        {
            throw new InvalidOperationException($"Source '{source.Id}' does not require credentials.");
        }

        return secretStore.SetAsync(GetKey(source.Id), credential, cancellationToken);
    }

    /// <summary>Removes a source credential.</summary>
    public ValueTask RemoveAsync(MapSourceDefinition source, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        return secretStore.RemoveAsync(GetKey(source.Id), cancellationToken);
    }

    /// <summary>Tests the securely stored credential for a source.</summary>
    public async ValueTask<bool> TestAsync(MapSourceDefinition source, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        var credential = await secretStore.GetAsync(GetKey(source.Id), cancellationToken).ConfigureAwait(false);
        return !string.IsNullOrEmpty(credential)
               && await credentialTester.TestAsync(source, credential, cancellationToken).ConfigureAwait(false);
    }

    private static string GetKey(string sourceId) => $"maps.credentials.{sourceId}";
}
