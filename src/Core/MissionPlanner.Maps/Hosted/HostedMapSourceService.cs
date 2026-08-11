using System.Net;
using System.Net.Http.Headers;
using MissionPlanner.Maps.Catalog;
using MissionPlanner.Maps.Credentials;
using MissionPlanner.Maps.Policy;

namespace MissionPlanner.Maps.Hosted;

/// <summary>Gates hosted sources on credentials and policy and prepares redaction-safe requests.</summary>
public sealed class HostedMapSourceService(MapCatalog catalog, IMapSecretStore secretStore, IMapPolicyEvaluator policyEvaluator)
{
    /// <summary>Gets current availability, policy, and attribution for a hosted source.</summary>
    public async ValueTask<HostedMapSourceState> GetStateAsync(string sourceId, CancellationToken cancellationToken = default)
    {
        var source = FindSource(sourceId);
        var policy = catalog.Policies.Single(item => item.Id == source.PolicyId);
        var credentialConfigured = source.CredentialRequirement == MapCredentialRequirement.None
                                   || !string.IsNullOrWhiteSpace(await secretStore.GetAsync(GetCredentialKey(source.Id), cancellationToken).ConfigureAwait(false));
        var interactive = policyEvaluator.Evaluate(source, policy, MapOperation.InteractiveUse);
        var cache = policyEvaluator.Evaluate(source, policy, MapOperation.ClientDiskCache);
        var pack = policyEvaluator.Evaluate(source, policy, MapOperation.OfflineAreaDownload);
        var attributions = source.AttributionIds.Select(id => catalog.Attributions.Single(item => item.Id == id)).ToArray();
        var summary = $"Interactive: {Decision(interactive)}; HTTP cache: {Decision(cache)}; offline area: {Decision(pack)}; proxy/redistribution: denied.";
        return new HostedMapSourceState(source, credentialConfigured, credentialConfigured && interactive.IsAllowed, summary, attributions);
    }

    /// <summary>Creates an authorized tile request without placing secrets in catalog state.</summary>
    public async ValueTask<HttpRequestMessage> CreateRequestAsync(string sourceId, int zoom, int column, int row, CancellationToken cancellationToken = default)
    {
        var source = FindSource(sourceId);
        var credential = await secretStore.GetAsync(GetCredentialKey(source.Id), cancellationToken).ConfigureAwait(false);
        if (source.CredentialRequirement != MapCredentialRequirement.None && string.IsNullOrWhiteSpace(credential))
        {
            throw new HostedMapException(HostedMapFailureKind.MissingCredential, $"Credentials are not configured for {source.DisplayName}.");
        }

        var endpoint = source.UriTemplate!.Replace("{z}", zoom.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal)
            .Replace("{x}", column.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal)
            .Replace("{y}", row.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal);
        var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
        if (source.AuthenticationStrategy == MapAuthenticationStrategy.HeaderApiKey)
        {
            request.Headers.TryAddWithoutValidation(source.AuthenticationName!, credential);
        }
        else if (source.AuthenticationStrategy == MapAuthenticationStrategy.AuthorizationBearer)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue(source.AuthenticationName ?? "Bearer", credential);
        }
        else if (source.AuthenticationStrategy == MapAuthenticationStrategy.QueryApiKey)
        {
            request.RequestUri = AppendSecret(request.RequestUri!, source.AuthenticationName!, credential!);
        }

        return request;
    }

    /// <summary>Converts an HTTP or network error into a distinct redaction-safe provider failure.</summary>
    public static HostedMapException ClassifyFailure(Exception exception, HttpStatusCode? statusCode = null)
    {
        ArgumentNullException.ThrowIfNull(exception);
        var kind = statusCode switch
        {
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => HostedMapFailureKind.Unauthorized,
            HttpStatusCode.TooManyRequests => HostedMapFailureKind.RateLimited,
            var _ when exception is HttpRequestException => HostedMapFailureKind.Network,
            var _ => HostedMapFailureKind.Provider
        };
        var message = kind switch
        {
            HostedMapFailureKind.Unauthorized => "The map provider rejected the configured credential or account permissions.",
            HostedMapFailureKind.RateLimited => "The map provider rate limit or account quota was reached.",
            HostedMapFailureKind.Network => "The map provider could not be reached.",
            var _ => "The map provider returned an unexpected response."
        };
        return new HostedMapException(kind, message, exception);
    }

    private MapSourceDefinition FindSource(string sourceId) => catalog.Sources.SingleOrDefault(item => item.Id == sourceId)
                                                               ?? throw new KeyNotFoundException($"Hosted map source '{sourceId}' is not in the catalog.");

    private static string GetCredentialKey(string sourceId) => $"maps.credentials.{sourceId}";
    private static string Decision(MapPolicyDecision decision) => decision.IsAllowed ? "allowed" : $"denied ({decision.PolicyId})";

    private static Uri AppendSecret(Uri endpoint, string name, string value)
    {
        var builder = new UriBuilder(endpoint);
        builder.Query = string.IsNullOrEmpty(builder.Query) ? $"{name}={Uri.EscapeDataString(value)}" : $"{builder.Query.TrimStart('?')}&{name}={Uri.EscapeDataString(value)}";
        return builder.Uri;
    }
}
