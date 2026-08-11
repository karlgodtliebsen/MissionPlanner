using BruTile;
using BruTile.Predefined;
using Mapsui.Layers;
using Mapsui.Tiling;
using Mapsui.Tiling.Layers;
using MissionPlanner.Maps.Catalog;
using MissionPlanner.Maps.Hosted;
using MissionPlanner.Maps.Http;
using MissionPlanner.Maps.Sources;

namespace MissionPlanner.App.Maps;

/// <summary>Identifies a normal Mapsui source-creation outcome.</summary>
public enum MapBasemapCreationStatus
{
    /// <summary>The layer was created.</summary>
    Success,
    /// <summary>The current raster renderer does not support the source.</summary>
    Unsupported,
    /// <summary>Reviewed policy denied use.</summary>
    PolicyDenied,
    /// <summary>A required credential is missing.</summary>
    CredentialMissing,
    /// <summary>The endpoint or archive is unavailable.</summary>
    SourceUnavailable,
    /// <summary>The source definition is invalid.</summary>
    InvalidConfiguration,
    /// <summary>The renderer could not construct the source.</summary>
    RendererFailure,
    /// <summary>Creation was cancelled.</summary>
    Cancelled
}

/// <summary>Contains a created layer or a typed ordinary failure.</summary>
/// <param name="Status">Creation outcome.</param>
/// <param name="Layer">Created layer on success.</param>
/// <param name="Message">Presentation-safe detail.</param>
public sealed record MapBasemapCreationResult(MapBasemapCreationStatus Status, ILayer? Layer, string? Message = null)
{
    /// <summary>Gets whether a usable layer was created.</summary>
    public bool IsSuccess => Status == MapBasemapCreationStatus.Success && Layer is not null;
}

/// <summary>Creates Mapsui basemaps from renderer-neutral resolved sources.</summary>
public interface IMapsuiBasemapFactory
{
    /// <summary>Creates a basemap without performing source selection.</summary>
    ValueTask<MapBasemapCreationResult> CreateAsync(ResolvedMapSource source, CancellationToken cancellationToken = default);
}

/// <summary>Routes supported raster sources to the appropriate production adapter.</summary>
public sealed class CompositeMapsuiBasemapFactory(
    MapsuiHostedBasemapFactory hostedFactory,
    MapsuiMbTilesSourceFactory mbTilesFactory,
    IMapHttpClientFactory httpClientFactory) : IMapsuiBasemapFactory
{
    /// <summary>Stable identity assigned to the single basemap slot.</summary>
    public const string BasemapLayerName = "MissionPlanner.Basemap";

    /// <inheritdoc />
    public async ValueTask<MapBasemapCreationResult> CreateAsync(ResolvedMapSource source, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            ILayer? layer;
            if (source.Definition.AccessKind == MapAccessKind.Blank)
                layer = new MemoryLayer();
            else if (source.Definition.ArchiveFormat == MapArchiveFormat.MbTiles && source.Origin == MapSourceOrigin.InstalledPack)
                layer = mbTilesFactory.Create(source);
            else if (source.Origin == MapSourceOrigin.Custom && source.Definition.AccessKind is MapAccessKind.HttpXyz or MapAccessKind.HttpTms)
                layer = CreateHttpRaster(source, httpClientFactory.CreateClient());
            else if (source.Origin == MapSourceOrigin.Custom)
                return Unsupported(source, "Custom WMS/WMTS rendering is not production-integrated.");
            else if (source.Definition.CredentialRequirement != MapCredentialRequirement.None)
                layer = await hostedFactory.CreateAsync(source, cancellationToken).ConfigureAwait(false);
            else if (source.Origin == MapSourceOrigin.Catalog && source.Definition.AccessKind == MapAccessKind.HttpXyz)
                layer = CreateBuiltIn(source);
            else
                return Unsupported(source, "The source access or archive format has no Mapsui raster adapter.");

            layer.Name = BasemapLayerName;
            return new(MapBasemapCreationStatus.Success, layer);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new(MapBasemapCreationStatus.Cancelled, null, "Basemap creation was cancelled.");
        }
        catch (HostedMapException exception) when (exception.Kind == HostedMapFailureKind.MissingCredential)
        {
            return new(MapBasemapCreationStatus.CredentialMissing, null, exception.Message);
        }
        catch (FileNotFoundException exception)
        {
            return new(MapBasemapCreationStatus.SourceUnavailable, null, exception.Message);
        }
        catch (Exception exception)
        {
            return new(MapBasemapCreationStatus.RendererFailure, null, exception.Message);
        }
    }

    private static ILayer CreateBuiltIn(ResolvedMapSource source) => source.Id switch
    {
        "osm-standard" => OpenStreetMap.CreateTileLayer(),
        "esri-world-topo" => new TileLayer(KnownTileSources.Create(KnownTileSource.EsriWorldTopo)),
        "esri-world-physical" => new TileLayer(KnownTileSources.Create(KnownTileSource.EsriWorldPhysical)),
        "esri-world-shaded-relief" => new TileLayer(KnownTileSources.Create(KnownTileSource.EsriWorldShadedRelief)),
        "esri-world-dark-gray" => new TileLayer(KnownTileSources.Create(KnownTileSource.EsriWorldDarkGrayBase)),
        _ => throw new NotSupportedException($"Built-in map source '{source.Id}' has no Mapsui adapter.")
    };

    private static ILayer CreateHttpRaster(ResolvedMapSource source, HttpClient client) =>
        new TileLayer(new ResolvedHttpTileSource(source, client));

    private static MapBasemapCreationResult Unsupported(ResolvedMapSource source, string detail) =>
        new(MapBasemapCreationStatus.Unsupported, null, $"Map source '{source.Id}' is unsupported. {detail}");

    private sealed class ResolvedHttpTileSource : ILocalTileSource, IDisposable
    {
        private readonly ResolvedMapSource source;
        private readonly HttpClient client;

        public ResolvedHttpTileSource(ResolvedMapSource source, HttpClient client)
        {
            this.source = source;
            this.client = client;
            Schema = new GlobalSphericalMercator(source.Definition.DisplayName,
                source.Definition.AccessKind == MapAccessKind.HttpTms ? YAxis.TMS : YAxis.OSM,
                source.Definition.MinimumZoom,
                source.Definition.MaximumZoom,
                source.Definition.ContentFormat.ToString());
            Name = source.Definition.DisplayName;
            Attribution = new Attribution(string.Join(" · ", source.Attribution.Select(item => item.Text)), string.Empty);
        }

        public ITileSchema Schema { get; }
        public string Name { get; }
        public Attribution Attribution { get; }

        public Task<byte[]?> GetTileAsync(TileInfo tileInfo)
        {
            var endpoint = source.Location!
                .Replace("{z}", tileInfo.Index.Level.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase)
                .Replace("{x}", tileInfo.Index.Col.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase)
                .Replace("{y}", tileInfo.Index.Row.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase);
            return client.GetByteArrayAsync(endpoint).ContinueWith<byte[]?>(task => task.Result, TaskScheduler.Default);
        }

        public void Dispose() => client.Dispose();
    }
}

/// <summary>Compatibility name for the stable basemap layer identity.</summary>
public static class MapsuiBasemapFactory
{
    /// <summary>Stable identity assigned to the single basemap slot.</summary>
    public const string BasemapLayerName = CompositeMapsuiBasemapFactory.BasemapLayerName;
}
