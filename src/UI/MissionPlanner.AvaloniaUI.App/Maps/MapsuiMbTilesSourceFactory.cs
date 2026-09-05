using BruTile;
using BruTile.Predefined;
using Mapsui.Layers;
using Mapsui.Tiling.Layers;
using Microsoft.Data.Sqlite;
using MissionPlanner.Maps.Offline;
using MissionPlanner.Maps.Sources;

namespace MissionPlanner.AvaloniaUI.App.Maps;

/// <summary>Creates read-only Mapsui layers for validated raster MBTiles packs.</summary>
public sealed class MapsuiMbTilesSourceFactory
{
    /// <summary>Creates a basemap layer for an installed pack.</summary>
    public ILayer Create(InstalledOfflineMapPack pack)
    {
        ArgumentNullException.ThrowIfNull(pack);
        var layer = new TileLayer(new ReadOnlyMbTilesSource(pack)) { Name = MapsuiBasemapFactory.BasemapLayerName };
        return layer;
    }

    /// <summary>Creates a basemap layer from an already resolved installed-pack source.</summary>
    public ILayer Create(ResolvedMapSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (string.IsNullOrWhiteSpace(source.Location) || !File.Exists(source.Location))
        {
            throw new FileNotFoundException("The resolved MBTiles archive is unavailable.", source.Location);
        }

        var parts = source.Id.Split(':', 3);
        var manifest = new OfflineMapPackManifest(
            parts.Length > 1 ? parts[1] : source.Id,
            parts.Length > 2 ? parts[2] : "resolved",
            source.Definition.DisplayName,
            Path.GetFileName(source.Location),
            string.Empty,
            new FileInfo(source.Location).Length,
            new(-180, -85, 180, 85),
            source.Definition.MinimumZoom,
            source.Definition.MaximumZoom,
            "EPSG:3857",
            source.Definition.ContentFormat switch
            {
                MissionPlanner.Maps.Catalog.MapTileContentFormat.RasterJpeg => "jpg",
                MissionPlanner.Maps.Catalog.MapTileContentFormat.RasterWebp => "webp",
                _ => "png"
            },
            string.Join(" · ", source.Attribution.Select(item => item.Text)),
            string.Empty);
        return Create(new InstalledOfflineMapPack(manifest, Path.GetDirectoryName(source.Location)!, source.Location));
    }

    private sealed class ReadOnlyMbTilesSource : ILocalTileSource, IDisposable
    {
        private readonly string connectionString;

        public ReadOnlyMbTilesSource(InstalledOfflineMapPack pack)
        {
            Name = pack.Manifest.DisplayName;
            Attribution = new Attribution(pack.Manifest.Attribution, string.Empty);
            Schema = new GlobalSphericalMercator(Name, YAxis.TMS, pack.Manifest.MinimumZoom, pack.Manifest.MaximumZoom, pack.Manifest.RasterFormat);
            connectionString = new SqliteConnectionStringBuilder { DataSource = pack.ArchivePath, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString();
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
            await using var connection = new SqliteConnection(connectionString);
            await connection.OpenAsync().ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT tile_data FROM tiles WHERE zoom_level = $zoom AND tile_column = $column AND tile_row = $row LIMIT 1";
            command.Parameters.AddWithValue("$zoom", tileInfo.Index.Level);
            command.Parameters.AddWithValue("$column", tileInfo.Index.Col);
            command.Parameters.AddWithValue("$row", tileInfo.Index.Row);
            return await command.ExecuteScalarAsync().ConfigureAwait(false) as byte[];
        }

        public void Dispose()
        {
        }
    }
}
