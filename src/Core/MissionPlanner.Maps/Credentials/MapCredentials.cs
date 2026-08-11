using MissionPlanner.Maps.Catalog;

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

/// <summary>Tests a map credential without exposing it to UI state.</summary>
public interface IMapCredentialTester
{
    /// <summary>Tests a credential for a source.</summary>
    ValueTask<bool> TestAsync(MapSourceDefinition source, string credential, CancellationToken cancellationToken = default);
}

/// <summary>Describes whether the credential required by a source is configured.</summary>
/// <param name="Requirement">Credential requirement.</param>
/// <param name="IsConfigured">Whether a secret is present.</param>
public sealed record MapCredentialState(MapCredentialRequirement Requirement, bool IsConfigured);

/// <summary>Manages source credentials without returning secrets to callers.</summary>
public sealed class MapCredentialService(IMapSecretStore secretStore, IMapCredentialTester credentialTester)
{
    /// <summary>Gets configuration state for a source.</summary>
    public async ValueTask<MapCredentialState> GetStateAsync(MapSourceDefinition source, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.CredentialRequirement == MapCredentialRequirement.None)
            return new(source.CredentialRequirement, true);
        return new(source.CredentialRequirement, !string.IsNullOrEmpty(await secretStore.GetAsync(GetKey(source.Id), cancellationToken).ConfigureAwait(false)));
    }

    /// <summary>Stores a source credential in secure storage.</summary>
    public ValueTask SetAsync(MapSourceDefinition source, string credential, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(credential);
        if (source.CredentialRequirement == MapCredentialRequirement.None)
            throw new InvalidOperationException($"Source '{source.Id}' does not require credentials.");
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

/// <summary>Redacts secrets and sensitive query parameters from map diagnostics.</summary>
public static class MapDiagnosticRedactor
{
    private static readonly string[] SensitiveNames = ["access_token", "api_key", "apikey", "key", "token", "password"];

    /// <summary>Redacts a known secret and sensitive URI query values.</summary>
    public static string Redact(string value, string? secret = null)
    {
        ArgumentNullException.ThrowIfNull(value);
        var redacted = string.IsNullOrEmpty(secret) ? value : value.Replace(secret, "[REDACTED]", StringComparison.Ordinal);
        foreach (var name in SensitiveNames)
            redacted = System.Text.RegularExpressions.Regex.Replace(redacted, $"(?i)([?&]{System.Text.RegularExpressions.Regex.Escape(name)}=)[^&#]*", "$1[REDACTED]");
        return redacted;
    }
}
