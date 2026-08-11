using BruTile;
using BruTile.Predefined;
using Mapsui.Layers;
using Mapsui.Tiling.Layers;
using Microsoft.Data.Sqlite;
using MissionPlanner.Maps.Offline;

namespace MissionPlanner.App.Maps;

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

        public ITileSchema Schema { get; }
        public string Name { get; }
        public Attribution Attribution { get; }

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
