using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using MissionPlanner.Maps.Catalog;
using MissionPlanner.Maps.Credentials;
using MissionPlanner.Maps.Policy;
using MissionPlanner.Maps.Sources;

namespace MissionPlanner.Maps.Http;

/// <summary>Default policy-aware, coalescing HTTP resource fetcher.</summary>
public sealed class MapHttpResourceFetcher(
    IMapHttpClientFactory httpClientFactory,
    MapHttpDiskCache cache,
    IMapPolicyEvaluator policyEvaluator,
    IMapSecretStore secretStore,
    IMapHttpRuntimeSettings runtimeSettings) : IMapHttpResourceFetcher
{
    private readonly ConcurrentDictionary<string, Lazy<Task<MapHttpFetchResult>>> pending = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public async ValueTask<MapHttpFetchResult> FetchAsync(MapHttpFetchRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var operation = pending.GetOrAdd($"{request.Source.Id}\n{request.CacheKey}", _ => new Lazy<Task<MapHttpFetchResult>>(() => FetchCoreAsync(request, cancellationToken), LazyThreadSafetyMode.ExecutionAndPublication));
        try
        {
            return await operation.Value.ConfigureAwait(false);
        }
        finally
        {
            pending.TryRemove(new KeyValuePair<string, Lazy<Task<MapHttpFetchResult>>>($"{request.Source.Id}\n{request.CacheKey}", operation));
        }
    }

    private async Task<MapHttpFetchResult> FetchCoreAsync(MapHttpFetchRequest request, CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var interactive = policyEvaluator.Evaluate(request.Source.Definition, request.Source.EffectivePolicy, MapOperation.InteractiveUse);
            if (!interactive.IsAllowed)
            {
                return Failure(MapHttpFetchStatus.PolicyDenied, interactive.Reason);
            }

            var cacheDecision = policyEvaluator.Evaluate(request.Source.Definition, request.Source.EffectivePolicy, MapOperation.ClientDiskCache);
            var useCache = runtimeSettings.CacheEnabled && cacheDecision.IsAllowed;
            cache.BudgetBytes = runtimeSettings.CacheLimitBytes;
            var cacheNamespace = new MapCacheNamespace(request.Source.Id, request.Source.DataProduct.Id, request.ResourceKind.ToString());
            var cached = useCache ? await cache.ReadAsync(cacheNamespace, request.CacheKey, cancellationToken).ConfigureAwait(false) : null;
            if (cached is { Metadata.ExpiresAt: { } expires } && expires > DateTimeOffset.UtcNow)
            {
                return new MapHttpFetchResult(MapHttpFetchStatus.Success, cached.Value.Content, true);
            }

            using var message = new HttpRequestMessage(HttpMethod.Get, request.Uri);
            if (cached is not null)
            {
                MapHttpDiskCache.ApplyValidators(message, cached.Value.Metadata);
            }

            if (request.Headers is not null)
            {
                foreach (var header in request.Headers)
                {
                    message.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }
            }

            var credentialResult = await ApplyCredentialAsync(message, request.Source, cancellationToken).ConfigureAwait(false);
            if (credentialResult is not null)
            {
                return credentialResult;
            }

            using var client = httpClientFactory.CreateClient();
            using var response = await client.SendAsync(message, HttpCompletionOption.ResponseContentRead, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.NotModified && cached is not null)
            {
                if (useCache)
                {
                    await cache.WriteAsync(cacheNamespace, request.CacheKey, cached.Value.Content, MapHttpDiskCache.CreateMetadata(response), cancellationToken).ConfigureAwait(false);
                }

                return new MapHttpFetchResult(MapHttpFetchStatus.Success, cached.Value.Content, true);
            }

            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                return Failure(MapHttpFetchStatus.Unauthorized, "The map provider rejected the configured credential or account permissions.");
            }

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                return Failure(MapHttpFetchStatus.RateLimited, "The map provider rate limit or quota was reached.");
            }

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return Failure(MapHttpFetchStatus.NotFound, "The requested map resource was not found.");
            }

            if (!response.IsSuccessStatusCode)
            {
                return Failure(MapHttpFetchStatus.NetworkFailure, $"The map provider returned HTTP {(int)response.StatusCode}.");
            }

            var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
            if (useCache && response.Headers.CacheControl?.NoStore != true)
            {
                await cache.WriteAsync(cacheNamespace, request.CacheKey, bytes, MapHttpDiskCache.CreateMetadata(response), cancellationToken).ConfigureAwait(false);
            }

            return new MapHttpFetchResult(MapHttpFetchStatus.Success, bytes, false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Failure(MapHttpFetchStatus.Cancelled, "Map resource request was cancelled.");
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException or TaskCanceledException)
        {
            return Failure(MapHttpFetchStatus.NetworkFailure, "The map provider could not be reached.");
        }
    }

    private async ValueTask<MapHttpFetchResult?> ApplyCredentialAsync(HttpRequestMessage request, ResolvedMapSource source, CancellationToken cancellationToken)
    {
        if (source.Definition.CredentialRequirement == MapCredentialRequirement.None)
        {
            return null;
        }

        var credential = await secretStore.GetAsync($"maps.credentials.{source.Id}", cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(credential))
        {
            return Failure(MapHttpFetchStatus.CredentialMissing, $"Credentials are not configured for {source.Definition.DisplayName}.");
        }

        switch (source.Definition.AuthenticationStrategy)
        {
            case MapAuthenticationStrategy.QueryApiKey:
                var builder = new UriBuilder(request.RequestUri!);
                var parameter = $"{Uri.EscapeDataString(source.Definition.AuthenticationName!)}={Uri.EscapeDataString(credential)}";
                builder.Query = string.IsNullOrEmpty(builder.Query) ? parameter : $"{builder.Query.TrimStart('?')}&{parameter}";
                request.RequestUri = builder.Uri;
                break;
            case MapAuthenticationStrategy.AuthorizationBearer:
                request.Headers.Authorization = new AuthenticationHeaderValue(source.Definition.AuthenticationName ?? "Bearer", credential);
                break;
            case MapAuthenticationStrategy.HeaderApiKey:
                request.Headers.TryAddWithoutValidation(source.Definition.AuthenticationName!, credential);
                break;
            default:
                return Failure(MapHttpFetchStatus.CredentialMissing, "The source has no reviewed credential injection strategy.");
        }

        return null;
    }

    private static MapHttpFetchResult Failure(MapHttpFetchStatus status, string message) => new(status, null, false, message);
}
