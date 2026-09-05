using BruTile;
using BruTile.Predefined;
using Mapsui.Layers;
using Mapsui.Tiling.Layers;
using MissionPlanner.Maps.Catalog;
using MissionPlanner.Maps.Http;
using MissionPlanner.Maps.Sources;

namespace MissionPlanner.AvaloniaUI.App.Maps;

/// <summary>Routes supported raster sources to the appropriate production adapter.</summary>
public sealed class CompositeMapsuiBasemapFactory(MapsuiMbTilesSourceFactory mbTilesFactory, IMapHttpResourceFetcher resourceFetcher) : IMapsuiBasemapFactory
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
            {
                layer = new MemoryLayer();
            }
            else if (source.Definition.ArchiveFormat == MapArchiveFormat.MbTiles && source.Origin == MapSourceOrigin.InstalledPack)
            {
                layer = mbTilesFactory.Create(source);
            }
            else if (source.Origin == MapSourceOrigin.Custom && source.Definition.AccessKind is MapAccessKind.HttpXyz or MapAccessKind.HttpTms)
            {
                layer = CreateHttpRaster(source, resourceFetcher);
            }
            else if (source.Origin == MapSourceOrigin.Custom)
            {
                return Unsupported(source, "Custom WMS/WMTS rendering is not production-integrated.");
            }
            else if (source.Origin == MapSourceOrigin.Catalog && source.Definition.AccessKind is MapAccessKind.HttpXyz or MapAccessKind.HttpTms)
            {
                layer = CreateHttpRaster(source, resourceFetcher);
            }
            else
            {
                return Unsupported(source, "The source access or archive format has no Mapsui raster adapter.");
            }

            layer.Name = BasemapLayerName;
            return new MapBasemapCreationResult(MapBasemapCreationStatus.Success, layer);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new MapBasemapCreationResult(MapBasemapCreationStatus.Cancelled, null, "Basemap creation was cancelled.");
        }
        catch (FileNotFoundException exception)
        {
            return new MapBasemapCreationResult(MapBasemapCreationStatus.SourceUnavailable, null, exception.Message);
        }
        catch (Exception exception)
        {
            return new MapBasemapCreationResult(MapBasemapCreationStatus.RendererFailure, null, exception.Message);
        }
    }

    private static ILayer CreateHttpRaster(ResolvedMapSource source, IMapHttpResourceFetcher fetcher)
    {
        return new TileLayer(new ResolvedHttpTileSource(source, fetcher));
    }

    private static MapBasemapCreationResult Unsupported(ResolvedMapSource source, string detail)
    {
        return new MapBasemapCreationResult(MapBasemapCreationStatus.Unsupported, null, $"Map source '{source.Id}' is unsupported. {detail}");
    }

    private sealed class ResolvedHttpTileSource : ILocalTileSource
    {
        private readonly ResolvedMapSource source;
        private readonly IMapHttpResourceFetcher fetcher;

        public ResolvedHttpTileSource(ResolvedMapSource source, IMapHttpResourceFetcher fetcher)
        {
            this.source = source;
            this.fetcher = fetcher;
            Schema = new GlobalSphericalMercator(source.Definition.DisplayName,
                source.Definition.AccessKind == MapAccessKind.HttpTms ? YAxis.TMS : YAxis.OSM,
                source.Definition.MinimumZoom,
                source.Definition.MaximumZoom,
                source.Definition.ContentFormat.ToString());
            Name = source.Definition.DisplayName;
            Attribution = new Attribution(string.Join(" · ", source.Attribution.Select(item => item.Text)), string.Empty);
        }

        public ITileSchema Schema
        {
            get;
        }
        public string Name
        {
            get;
        }
        public Attribution Attribution
        {
            get;
        }

        public async Task<byte[]?> GetTileAsync(TileInfo tileInfo)
        {
            var endpoint = source.Location!
                .Replace("{z}", tileInfo.Index.Level.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase)
                .Replace("{x}", tileInfo.Index.Col.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase)
                .Replace("{y}", tileInfo.Index.Row.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase);
            var result = await fetcher.FetchAsync(new MapHttpFetchRequest(source, new Uri(endpoint), MapHttpResourceKind.RasterTile, $"{tileInfo.Index.Level}/{tileInfo.Index.Col}/{tileInfo.Index.Row}")).ConfigureAwait(false);
            return result.Content;
        }
    }
}
