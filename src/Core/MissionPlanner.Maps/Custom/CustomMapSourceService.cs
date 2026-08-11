using MissionPlanner.Maps.Credentials;
using MissionPlanner.Maps.Http;

namespace MissionPlanner.Maps.Custom;

/// <summary>Provides add, edit, test, delete, and fallback APIs for custom sources.</summary>
public sealed class CustomMapSourceService(ICustomMapSourceStore store, IMapHttpClientFactory httpClientFactory)
{
    /// <summary>Adds or replaces a source.</summary>
    public async ValueTask SaveAsync(CustomMapSourceSettings source, CancellationToken cancellationToken = default)
    {
        CustomMapSourceValidator.ValidateAndThrow(source);
        var sources = (await store.LoadAsync(cancellationToken).ConfigureAwait(false)).Where(item => item.Id != source.Id).Append(source).ToArray();
        await store.SaveAsync(sources, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Deletes a source and returns the selected source ID or safe fallback.</summary>
    public async ValueTask<string> DeleteAsync(string sourceId, string selectedSourceId, CancellationToken cancellationToken = default)
    {
        var sources = (await store.LoadAsync(cancellationToken).ConfigureAwait(false)).Where(item => item.Id != sourceId).ToArray();
        await store.SaveAsync(sources, cancellationToken).ConfigureAwait(false);
        return selectedSourceId == sourceId ? "osm-standard" : selectedSourceId;
    }

    /// <summary>Tests a source endpoint with bounded cancellable HTTP and redacted status.</summary>
    public async ValueTask<CustomMapConnectionStatus> TestAsync(CustomMapSourceSettings source, CancellationToken cancellationToken = default)
    {
        CustomMapSourceValidator.ValidateAndThrow(source);
        try
        {
            using var client = httpClientFactory.CreateClient();
            var endpoint = BuildTestEndpoint(source);
            using var response = await client.GetAsync(endpoint, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            MapServiceMetadata? metadata = null;
            if (source.AccessKind is Catalog.MapAccessKind.Wms or Catalog.MapAccessKind.Wmts)
            {
                var xml = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                metadata = MapServiceMetadataParser.Parse(xml);
                if (!metadata.LayerNames.Contains(source.LayerName!, StringComparer.Ordinal))
                {
                    return new CustomMapConnectionStatus(false, $"Configured layer '{source.LayerName}' was not advertised.", DateTimeOffset.UtcNow, metadata);
                }
            }

            return new CustomMapConnectionStatus(true, "Connection succeeded.", DateTimeOffset.UtcNow, metadata);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return new CustomMapConnectionStatus(false, MapDiagnosticRedactor.Redact(exception.Message), DateTimeOffset.UtcNow, null);
        }
    }

    private static string BuildTestEndpoint(CustomMapSourceSettings source)
    {
        if (source.AccessKind is Catalog.MapAccessKind.HttpXyz or Catalog.MapAccessKind.HttpTms)
        {
            return source.Endpoint.Replace("{z}", source.MinimumZoom.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase).Replace("{x}", "0", StringComparison.OrdinalIgnoreCase).Replace("{y}", "0", StringComparison.OrdinalIgnoreCase);
        }

        var separator = source.Endpoint.Contains('?') ? '&' : '?';
        return source.AccessKind == Catalog.MapAccessKind.Wms
            ? $"{source.Endpoint}{separator}service=WMS&request=GetCapabilities"
            : $"{source.Endpoint}{separator}service=WMTS&request=GetCapabilities";
    }
}
