using System.Text.Json;
using MissionPlanner.Maps.Attribution;
using MissionPlanner.Maps.Catalog;
using MissionPlanner.Maps.Credentials;
using MissionPlanner.Maps.Http;
using MissionPlanner.Maps.Sources;

namespace MissionPlanner.Maps.Esri;

/// <summary>Resolves current Esri MapServer attribution with a conservative fallback.</summary>
public sealed class EsriAttributionResolver(MapCatalog catalog, IMapSourceResolver sourceResolver, IMapHttpResourceFetcher resourceFetcher) : IMapDynamicAttributionResolver
{
    /// <inheritdoc />
    public async ValueTask<IReadOnlyCollection<MapAttributionEntry>> ResolveAsync(string contributorId, CancellationToken cancellationToken = default)
    {
        var source = catalog.Sources.SingleOrDefault(item => item.Id == contributorId && item.ProductId.StartsWith("esri-", StringComparison.Ordinal));
        if (source?.UriTemplate is null) return [];
        var fallback = catalog.Attributions.Single(item => item.Id == "esri");
        try
        {
            var tileMarker = source.UriTemplate.IndexOf("/tile/", StringComparison.OrdinalIgnoreCase);
            if (tileMarker < 0) return [fallback];
            var metadataUri = source.UriTemplate[..tileMarker] + "?f=json";
            var resolved = await sourceResolver.ResolveAsync(source.Id, cancellationToken).ConfigureAwait(false);
            if (!resolved.IsSuccess) return [fallback];
            var response = await resourceFetcher.FetchAsync(new(resolved.Source!, new Uri(metadataUri), MapHttpResourceKind.AttributionMetadata, metadataUri), cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccess) return [fallback];
            using var document = JsonDocument.Parse(response.Content!);
            var copyright = document.RootElement.TryGetProperty("copyrightText", out var value) ? value.GetString() : null;
            if (string.IsNullOrWhiteSpace(copyright)) return [fallback];
            return [fallback, new($"esri-service-{source.Id}", copyright, new Uri(metadataUri), true, true)];
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch { return [fallback]; }
    }
}

/// <summary>Builds optional authenticated Esri requests without persisting or logging tokens.</summary>
public static class EsriRequestUriBuilder
{
    /// <summary>Appends a token to an Esri URI at the request boundary.</summary>
    public static Uri WithToken(Uri endpoint, string token)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        var builder = new UriBuilder(endpoint);
        builder.Query = string.IsNullOrEmpty(builder.Query) ? $"token={Uri.EscapeDataString(token)}" : $"{builder.Query.TrimStart('?')}&token={Uri.EscapeDataString(token)}";
        return builder.Uri;
    }

    /// <summary>Returns a redacted diagnostic form of an authenticated URI.</summary>
    public static string ToDiagnosticString(Uri endpoint) => MapDiagnosticRedactor.Redact(endpoint.ToString());
}
