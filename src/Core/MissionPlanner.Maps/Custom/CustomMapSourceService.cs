using System.Text.Json;
using MissionPlanner.Maps.Credentials;
using MissionPlanner.Maps.Http;

namespace MissionPlanner.Maps.Custom;

/// <summary>Persists non-secret custom source settings.</summary>
public interface ICustomMapSourceStore
{
    /// <summary>Loads configured sources.</summary>
    ValueTask<IReadOnlyList<CustomMapSourceSettings>> LoadAsync(CancellationToken cancellationToken = default);
    /// <summary>Saves configured sources atomically.</summary>
    ValueTask SaveAsync(IReadOnlyList<CustomMapSourceSettings> sources, CancellationToken cancellationToken = default);
}

/// <summary>Stores custom map sources in an atomic JSON document.</summary>
public sealed class JsonCustomMapSourceStore(string filePath) : ICustomMapSourceStore
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true, WriteIndented = true };

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<CustomMapSourceSettings>> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath)) return [];
        return JsonSerializer.Deserialize<CustomMapSourceSettings[]>(await File.ReadAllTextAsync(filePath, cancellationToken).ConfigureAwait(false), Options) ?? [];
    }

    /// <inheritdoc />
    public async ValueTask SaveAsync(IReadOnlyList<CustomMapSourceSettings> sources, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sources);
        foreach (var source in sources) CustomMapSourceValidator.ValidateAndThrow(source);
        var fullPath = Path.GetFullPath(filePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        var staging = fullPath + $".staging-{Guid.NewGuid():N}";
        try
        {
            await File.WriteAllTextAsync(staging, JsonSerializer.Serialize(sources.OrderBy(item => item.Id, StringComparer.Ordinal), Options), cancellationToken).ConfigureAwait(false);
            File.Move(staging, fullPath, overwrite: true);
        }
        finally { if (File.Exists(staging)) File.Delete(staging); }
    }
}

/// <summary>Describes the most recent custom source connection test.</summary>
/// <param name="Succeeded">Whether the endpoint responded and metadata matched.</param>
/// <param name="Message">Redacted status text.</param>
/// <param name="TestedAt">Test timestamp.</param>
/// <param name="Metadata">Optional parsed WMS/WMTS metadata.</param>
public sealed record CustomMapConnectionStatus(bool Succeeded, string Message, DateTimeOffset TestedAt, MapServiceMetadata? Metadata);

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
                    return new(false, $"Configured layer '{source.LayerName}' was not advertised.", DateTimeOffset.UtcNow, metadata);
            }
            return new(true, "Connection succeeded.", DateTimeOffset.UtcNow, metadata);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return new(false, MapDiagnosticRedactor.Redact(exception.Message), DateTimeOffset.UtcNow, null);
        }
    }

    private static string BuildTestEndpoint(CustomMapSourceSettings source)
    {
        if (source.AccessKind is Catalog.MapAccessKind.HttpXyz or Catalog.MapAccessKind.HttpTms)
            return source.Endpoint.Replace("{z}", source.MinimumZoom.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase).Replace("{x}", "0", StringComparison.OrdinalIgnoreCase).Replace("{y}", "0", StringComparison.OrdinalIgnoreCase);
        var separator = source.Endpoint.Contains('?') ? '&' : '?';
        return source.AccessKind == Catalog.MapAccessKind.Wms
            ? $"{source.Endpoint}{separator}service=WMS&request=GetCapabilities"
            : $"{source.Endpoint}{separator}service=WMTS&request=GetCapabilities";
    }
}
